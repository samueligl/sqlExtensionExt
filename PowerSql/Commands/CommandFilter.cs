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
                Logger.Log("TAB key detected. Analyzing context for expansion...");
                if (CheckAsteriskAndQueueExpansion())
                {
                    // Bloqueamos el comportamiento normal del TAB instantáneamente
                    // El procesamiento real (acceso a BD) se realiza asíncronamente
                    return VSConstants.S_OK;
                }
                Logger.Log("TAB ignored. Resuming normal behavior.");
            }

            // Dejamos pasar la tecla
            return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool CheckAsteriskAndQueueExpansion()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var caretPosition = _textView.Caret.Position.BufferPosition;
            var line = caretPosition.GetContainingLine();
            string textBeforeCaret = line.GetText().Substring(0, caretPosition.Position - line.Start.Position);

            Logger.Log($"Caret Position: {caretPosition.Position}");
            Logger.Log($"Text before caret in line: '{textBeforeCaret}'");

            if (textBeforeCaret.TrimEnd().EndsWith("*"))
            {
                Logger.Log("Asterisk (*) found directly before caret.");
                string fullText = _textView.TextBuffer.CurrentSnapshot.GetText();
                string tableName = ExtractTableName(fullText, caretPosition.Position);

                if (!string.IsNullOrEmpty(tableName))
                {
                    Logger.Log($"Table resolved via Regex: '{tableName}'. Queuing async DB lookup...");

                    // Guardamos un snapshot del tracking point para saber exactamente dónde insertar
                    // después de que el hilo de background termine (por si el usuario movió el cursor)
                    var trackingPoint = _textView.TextSnapshot.CreateTrackingPoint(caretPosition.Position, PointTrackingMode.Positive);

                    // Lanzamos el proceso asíncrono usando la infraestructura del VS Threading Model
                    Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                    {
                        // 1. Ir a base de datos asíncronamente (fuera del hilo de UI)
                        string columnsFormatted = await SsmsConnectionService.GetFormattedColumnsAsync(tableName);

                        // 2. Volver al hilo principal para editar el documento
                        await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        if (!string.IsNullOrEmpty(columnsFormatted) && !columnsFormatted.Contains("-- Error"))
                        {
                            Logger.Log($"Successfully fetched formatting columns.");
                            // Reemplazar usando el punto guardado
                            ReplaceAsteriskAtTrackingPoint(trackingPoint, columnsFormatted);
                        }
                        else if(columnsFormatted != null && columnsFormatted.Contains("-- Error"))
                        {
                            Logger.Log($"Error from DB lookup: {columnsFormatted}");
                        }
                    });

                    // Retornamos true inmediatamente para bloquear el TAB, evitando el timeout
                    return true;
                }
                else
                {
                    Logger.Log("No table name could be parsed after FROM statement.");
                }
            }
            else
            {
                Logger.Log("No asterisk directly before caret. Ignoring.");
            }
            return false;
        }

        private void ReplaceAsteriskAtTrackingPoint(ITrackingPoint trackingPoint, string replacement)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            using (var edit = _textView.TextBuffer.CreateEdit())
            {
                // Obtenemos la posición actual basada en el tracking point guardado
                SnapshotPoint currentPoint = trackingPoint.GetPoint(edit.Snapshot);
                var line = currentPoint.GetContainingLine();
                string text = line.GetText().Substring(0, currentPoint.Position - line.Start.Position);
                int asteriskIndexInLine = text.LastIndexOf('*');

                if (asteriskIndexInLine >= 0)
                {
                    int startPos = line.Start.Position + asteriskIndexInLine;
                    edit.Replace(startPos, 1, replacement);
                    edit.Apply();
                }
            }
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
