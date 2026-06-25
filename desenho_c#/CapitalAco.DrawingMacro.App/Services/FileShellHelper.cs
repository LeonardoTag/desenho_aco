using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CapitalAco.DrawingMacro.App.Services
{
    // Operações de integração com o Explorer do Windows: abrir pastas e colocar arquivos na área de
    // transferência no formato CF_HDROP (o mesmo usado pelo Explorer ao pressionar Ctrl+C sobre um arquivo),
    // permitindo que o usuário cole o arquivo em qualquer lugar (pasta, e-mail, chat) com Ctrl+V.
    public static class FileShellHelper
    {
        public static void CopiarArquivoParaAreaDeTransferencia(string caminhoArquivo)
        {
            if (!File.Exists(caminhoArquivo)) return;

            var colecao = new StringCollection { caminhoArquivo };
            Clipboard.SetFileDropList(colecao);
        }

        public static void AbrirPasta(string caminhoPasta)
        {
            if (!Directory.Exists(caminhoPasta)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = caminhoPasta,
                UseShellExecute = true
            });
        }
    }
}
