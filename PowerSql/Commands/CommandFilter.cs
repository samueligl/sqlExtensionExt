using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using PowerSql.Services;

namespace PowerSql.Commands
{
    internal class CommandFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _textView;
        public IOleCommandTarget Next { get; set; }

        public CommandFilter(IWpfTextView textView)
        {
            _textView = textView;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            return Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            // Interceptamos TAB
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB)
            {
                if (TryProcessAsteriskExpansion())
                {
                    // Bloqueamos el comportamiento normal del TAB
                    return VSConstants.S_OK;
                }
            }

            // Dejamos pasar la tecla
            return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool TryProcessAsteriskExpansion()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var caretPosition = _textView.Caret.Position.BufferPosition;
            var line = caretPosition.GetContainingLine();
            string textBeforeCaret = line.GetText().Substring(0, caretPosition.Position - line.Start.Position);

            // Validamos si termina en '*' (ignorando espacios en blanco al final si fuera necesario)
            if (textBeforeCaret.TrimEnd().EndsWith("*"))
            {
                string fullText = _textView.TextBuffer.CurrentSnapshot.GetText();

                // 1. Detectar la tabla usando la Regex robusta
                string tableName = ExtractTableName(fullText, caretPosition.Position);

                if (!string.IsNullOrEmpty(tableName))
                {
                    // 2. Obtener las columnas usando nuestro servicio
                    string columnsFormatted = SsmsConnectionService.GetFormattedColumns(tableName);

                    if (!string.IsNullOrEmpty(columnsFormatted))
                    {
                        // 3. Reemplazar texto
                        ReplaceAsterisk(caretPosition, columnsFormatted);
                        return true;
                    }
                }
            }
            return false;
        }

        private string ExtractTableName(string sql, int caretPos)
        {
            string textAfterAsterisk = sql.Substring(caretPos);

            // Regex mejorada: Soporta saltos de línea, alias y corchetes
            var match = Regex.Match(textAfterAsterisk, @"\bFROM\b\s+((?:\[?[a-zA-Z0-9_]+\]?\.)*\[?[a-zA-Z0-9_]+\]?)",
                                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        private void ReplaceAsterisk(SnapshotPoint caretPosition, string replacement)
        {
            using (var edit = _textView.TextBuffer.CreateEdit())
            {
                // Buscamos la posición exacta del asterisco hacia atrás
                var line = caretPosition.GetContainingLine();
                string text = line.GetText().Substring(0, caretPosition.Position - line.Start.Position);
                int asteriskIndexInLine = text.LastIndexOf('*');

                if (asteriskIndexInLine >= 0)
                {
                    int startPos = line.Start.Position + asteriskIndexInLine;

                    // Reemplazamos exactamente el carácter '*'
                    edit.Replace(startPos, 1, replacement);
                    edit.Apply();
                }
            }
        }
    }
}
