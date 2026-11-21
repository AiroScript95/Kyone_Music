using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;

namespace KyoneMusic.Services
{
    public static class InstanciaUnica
    {
        private const string NomeMutex = "ProjectKitsune_SingleInstance";
        private const string NomePipe = "ProjectKitsune_Pipe";
        private static Mutex? _mutex;

        public static bool EPrimeira()
        {
            _mutex = new Mutex(true, NomeMutex, out bool criado);
            return criado;
        }

        public static void EnviarParaPrimeira(string? caminho)
        {
            try {
                using var cliente = new NamedPipeClientStream(".", NomePipe, PipeDirection.Out);
                cliente.Connect(2000);
                using var escritor = new StreamWriter(cliente, Encoding.UTF8);
                escritor.WriteLine(caminho ?? "");
            }
            catch (TimeoutException) { }
            catch (IOException) { }
        }

        public static void Escutar(Action<string?> aoReceber)
        {
            Task.Run(async () =>
            {
                while (true) {
                    try {
                        using var servidor = new NamedPipeServerStream(
                            NomePipe, PipeDirection.In, 1,
                            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        await servidor.WaitForConnectionAsync();
                        using var leitor = new StreamReader(servidor, Encoding.UTF8);
                        aoReceber(await leitor.ReadLineAsync());
                    }
                    catch (IOException) { await Task.Delay(100); }
                }
            });
        }
    }
}