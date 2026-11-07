using System.Security.Cryptography;

namespace StoreSync.UpdateApi.Services
{
    // Dosya yolları ve fiziksel dosya işlemleri burada yapılır.
    public class DemoFolderService : IDisposable
    {
        private readonly FileStream _instanceLock;
        public string RootDirectory { get; }
        public string SourceDirectory { get; }
        public string DestinationDirectory { get; }
        public string StatePath { get; }
        private string PartialDirectory { get; }

        public DemoFolderService(IWebHostEnvironment environment)
        {
            string? testDirectory = Environment.GetEnvironmentVariable("STORESYNC_DATA");
            if (string.IsNullOrWhiteSpace(testDirectory))
            {
                RootDirectory = Path.Combine(environment.ContentRootPath, "DemoData");
            }
            else
            {
                RootDirectory = testDirectory;
            }

            SourceDirectory = Path.Combine(RootDirectory, "IstanbulSunucu");
            DestinationDirectory = Path.Combine(RootDirectory, "UkraynaMagaza");
            string stateDirectory = Path.Combine(RootDirectory, ".storesync");
            PartialDirectory = Path.Combine(stateDirectory, "partial");
            StatePath = Path.Combine(stateDirectory, "state.json");

            Directory.CreateDirectory(SourceDirectory);
            Directory.CreateDirectory(DestinationDirectory);
            Directory.CreateDirectory(PartialDirectory);

            // Aynı demo verisini iki API'nin aynı anda değiştirmesini engeller.
            string lockPath = Path.Combine(stateDirectory, "instance.lock");
            _instanceLock = new FileStream(lockPath, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }

        public void Initialize()
        {
            string markerPath = Path.Combine(RootDirectory, ".storesync", "initialized");
            if (File.Exists(markerPath))
            {
                return;
            }

            CreateSampleFiles();
            File.WriteAllText(markerPath, "Örnek dosyalar oluşturuldu.");
        }

        public void CreateSampleFiles()
        {
            byte[] content = new byte[1024 * 1024];

            for (int number = 1; number <= 100; number++)
            {
                string fileName = "modul-" + number.ToString("000") + ".dat";
                string sourcePath = GetSourcePath(fileName);
                string destinationPath = GetDestinationPath(fileName);

                RandomNumberGenerator.Fill(content);
                File.WriteAllBytes(sourcePath, content);

                if (number <= 60)
                {
                    // İlk 60 dosya iki klasörde de aynı olsun.
                    File.Copy(sourcePath, destinationPath, true);
                }
                else if (number <= 70)
                {
                    // Sonraki 10 dosyanın adı/boyutu aynı, içeriği farklı olsun.
                    RandomNumberGenerator.Fill(content);
                    File.WriteAllBytes(destinationPath, content);
                }
                else
                {
                    // Son 30 dosya hedefte bulunmasın.
                    File.Delete(destinationPath);
                }
            }
        }

        public string[] GetSourceFiles()
        {
            string[] files = Directory.GetFiles(SourceDirectory);
            Array.Sort(files);
            return files;
        }

        public int GetDestinationCount()
        {
            string[] files = Directory.GetFiles(DestinationDirectory);
            return files.Length;
        }

        public string GetSourcePath(string fileName)
        {
            return GetSafePath(SourceDirectory, fileName);
        }

        public string GetDestinationPath(string fileName)
        {
            return GetSafePath(DestinationDirectory, fileName);
        }

        public string GetPartialPath(string fileName, string hash)
        {
            string partialFileName = fileName + "." + hash + ".part";
            return GetSafePath(PartialDirectory, partialFileName);
        }

        public static string CalculateHash(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = SHA256.HashData(stream);
                return Convert.ToHexString(hash);
            }
        }

        public bool FileMatches(string filePath, long expectedSize, string expectedHash)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Length != expectedSize)
            {
                return false;
            }

            string actualHash = CalculateHash(filePath);
            return actualHash == expectedHash;
        }

        private string GetSafePath(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Dosya adı boş olamaz.");
            }

            // İstek, dosya adı yerine başka klasöre giden bir yol içermemeli.
            bool invalidName = fileName != Path.GetFileName(fileName)
                || fileName.Contains('/') || fileName.Contains('\\')
                || fileName.Contains(':') || fileName == "." || fileName == "..";

            if (invalidName)
            {
                throw new ArgumentException("Geçersiz dosya adı.");
            }

            return Path.Combine(directory, fileName);
        }

        public void Dispose()
        {
            _instanceLock.Dispose();
        }
    }
}
