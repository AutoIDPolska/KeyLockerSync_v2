using System;
using System.IO;
using System.Text;

namespace KeyLockerSync.Services
{
    public class FileLogger : TextWriter, IDisposable
    {
        private readonly TextWriter _originalOut;
        private readonly StreamWriter _fileWriter;

        public FileLogger(string logFilePath)
        {
            _originalOut = Console.Out;
            try
            {
                // Otwieramy plik w trybie dopisywania (append)
                _fileWriter = new StreamWriter(logFilePath, true, Encoding.UTF8)
                {
                    // Automatyczne opróżnianie bufora po każdym zapisie
                    AutoFlush = true
                };
            }
            catch (Exception e)
            {
                _originalOut.WriteLine($"[FATAL] Nie można otworzyć pliku logów: {e.Message}");
                // Jeśli nie uda się otworzyć pliku, logowanie będzie działać tylko w konsoli
            }
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string message)
        {
            // Dodajemy znacznik czasu do każdej wiadomości
            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}";

            // Zapis do konsoli
            _originalOut.WriteLine(logMessage);

            // Zapis do pliku (jeśli plik został pomyślnie otwarty)
            _fileWriter?.WriteLine(logMessage);
        }

        // Metoda do zwolnienia zasobów
        public override void Close()
        {
            _fileWriter?.Close();
            base.Close();
        }
    }
}