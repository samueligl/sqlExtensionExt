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
    [ContentType("SQL Server Tools")] // El tipo de contenido específico de SSMS para query windows
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class SsmsEditorListener : IWpfTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            // Obtenemos el adaptador legacy (necesario para IOleCommandTarget)
            IVsTextView viewAdapter = AdapterService.GetViewAdapter(textView);
            if (viewAdapter != null)
            {
                // Registramos nuestro filtro de comandos para interceptar el teclado
                var filter = new CommandFilter(textView);
                viewAdapter.AddCommandFilter(filter, out IOleCommandTarget next);
                filter.Next = next; // Guardamos el siguiente comando en la cadena
            }
        }
    }
}
