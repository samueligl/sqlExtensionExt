using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using PowerSql.Commands;

namespace PowerSql.Listeners
{
    // Inyectamos nuestro código cuando se crea un editor de texto
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("SQL Server Tools")] // Tipo de contenido base real esperado
    [ContentType("text")]             // Fallback amplio en caso de que cambie en SSMS 22
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class SsmsEditorListener : IWpfTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                PowerSql.Services.Logger.Log($"--- TextViewCreated Event ---");
                PowerSql.Services.Logger.Log($"Buffer ContentType: {textView.TextBuffer.ContentType.TypeName}");
                PowerSql.Services.Logger.Log($"TextView Role: {string.Join(", ", textView.Roles)}");

                // Obtenemos el adaptador legacy (necesario para IOleCommandTarget)
                IVsTextView viewAdapter = AdapterService.GetViewAdapter(textView);
                if (viewAdapter != null)
                {
                    // Registramos nuestro filtro de comandos para interceptar el teclado
                    var filter = new CommandFilter(textView);
                    viewAdapter.AddCommandFilter(filter, out IOleCommandTarget next);
                    filter.Next = next; // Guardamos el siguiente comando en la cadena

                    PowerSql.Services.Logger.Log("CommandFilter successfully injected.");
                }
                else
                {
                    PowerSql.Services.Logger.Log("Warning: Could not get IVsTextView adapter. CommandFilter not injected.");
                }
            }
            catch (System.Exception ex)
            {
                PowerSql.Services.Logger.Log($"Error in TextViewCreated: {ex.Message}");
            }
        }
    }
}
