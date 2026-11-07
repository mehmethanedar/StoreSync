using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StoreSync.UpdateApi.Models;

namespace StoreSync.UpdateApi.Services
{
    public class TransferService : IDisposable
    {
        private readonly DemoFolderService _folderService;
        private readonly HttpClient _httpClient;
        private readonly string _sourceBaseUrl;

        // Web isteği ve arka plan işlemi aynı listeyi kullanır.
        // lock, bu listeyi aynı anda değiştirmelerini engeller.
        private readonly object _stateLock = new object();
        private TransferStatus _status = new TransferStatus();
        private bool _isTransferRequested;

        // Duraklat düğmesi bu nesne üzerinden indirmeyi durdurur.
        private CancellationTokenSource? _transferCancellation;

        public TransferService(DemoFolderService folderService,
            IHttpClientFactory httpClientFactory, IOptions<DemoOptions> options)
        {
            _folderService = folderService;
            _httpClient = httpClientFactory.CreateClient("SourceServer");
            _sourceBaseUrl = options.Value.SourceBaseUrl;

            LoadSavedStatus();

            if (_status.DelayMilliseconds != 0)
            {
                _status.DelayMilliseconds = 150;
            }

            // API aktarım sırasında kapandıysa açılınca devam etsin.
            if (_status.Phase == "Aktarılıyor" || _status.Phase == "Yeniden deneniyor"
                || _status.Phase == "Doğrulanıyor")
            {
                _isTransferRequested = true;
            }
        }

        // Ekranın istediği güncel bilgileri döndürür.
        public TransferStatus GetStatus()
        {
            lock (_stateLock)
            {
                TransferStatus result = _status.Copy();
                result.SourceFileCount = _folderService.GetSourceFiles().Length;
                result.DestinationFileCount = _folderService.GetDestinationCount();
                return result;
            }
        }

        // 1. Kaynak ve hedef dosyaları karşılaştır.
        public void Analyze()
        {
            lock (_stateLock)
            {
                EnsureTransferIsIdle();
                CreateTransferPlan();
            }
        }

        private void CreateTransferPlan()
        {
            TransferStatus plan = new TransferStatus();
            plan.DelayMilliseconds = _status.DelayMilliseconds;

            foreach (string sourcePath in _folderService.GetSourceFiles())
            {
                TransferFile file = new TransferFile();
                file.Name = Path.GetFileName(sourcePath);
                file.Size = new FileInfo(sourcePath).Length;
                file.Sha256 = DemoFolderService.CalculateHash(sourcePath);

                string destinationPath = _folderService.GetDestinationPath(file.Name);

                if (!File.Exists(destinationPath))
                {
                    file.Action = "Yeni";
                }
                else if (_folderService.FileMatches(destinationPath, file.Size, file.Sha256))
                {
                    file.Action = "Atla";
                    file.Phase = "Aynı";
                }
                else
                {
                    file.Action = "Güncelle";
                }

                plan.Files.Add(file);
            }

            plan.Phase = "Analiz tamamlandı";
            _status = plan;
            SaveStatus();
        }

        // 2. Aktarımı iste. Asıl indirmeyi TransferWorker yapacak.
        public void Start(bool isSlow)
        {
            lock (_stateLock)
            {
                bool noPendingFiles = _status.Files.Count == 0 || _status.RemainingFiles == 0;
                bool noActiveTransfer = !_isTransferRequested && _transferCancellation == null;

                if (noPendingFiles && noActiveTransfer)
                {
                    CreateTransferPlan();
                }

                _status.DelayMilliseconds = 0;
                if (isSlow)
                {
                    _status.DelayMilliseconds = 150;
                }
                _status.Error = null;

                if (_status.RemainingFiles > 0)
                {
                    _isTransferRequested = true;
                    _status.Phase = "Aktarılıyor";
                }
                else
                {
                    _isTransferRequested = false;
                    _status.Phase = "Tamamlandı";
                }

                SaveStatus();
            }
        }

        public void Pause()
        {
            lock (_stateLock)
            {
                _isTransferRequested = false;
                if (_transferCancellation != null)
                {
                    _transferCancellation.Cancel();
                }

                _status.Phase = "Duraklatıldı";
                SaveStatus();
            }
        }

        public void SetSpeed(bool isSlow)
        {
            lock (_stateLock)
            {
                _status.DelayMilliseconds = 0;
                if (isSlow)
                {
                    _status.DelayMilliseconds = 150;
                }
                SaveStatus();
            }
        }

        public void ResetDemo()
        {
            lock (_stateLock)
            {
                EnsureTransferIsIdle();
                _folderService.CreateSampleFiles();
                _status = new TransferStatus();
                SaveStatus();
            }
        }

        // 3. Arka plan işlemi, bekleyen dosyaları sırayla aktarır.
        public async Task ProcessAsync(CancellationToken stoppingToken)
        {
            CancellationTokenSource cancellation;

            lock (_stateLock)
            {
                if (!_isTransferRequested)
                {
                    return;
                }

                // Hem Duraklat hem API kapanışı bu indirmeyi durdurabilsin.
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _transferCancellation = cancellation;
            }

            try
            {
                while (true)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    TransferFile? nextFile = FindNextFile();
                    if (nextFile == null)
                    {
                        break;
                    }

                    await TransferFileAsync(nextFile, cancellation.Token);
                }

                lock (_stateLock)
                {
                    if (_isTransferRequested && !cancellation.IsCancellationRequested)
                    {
                        _isTransferRequested = false;
                        _status.Phase = "Tamamlandı";
                        _status.Error = null;
                        SaveStatus();
                    }
                }
            }
            catch (OperationCanceledException error)
            {
                // Duraklatma/kapanış normaldir. HTTP zaman aşımı varsa tekrar denenecek.
                if (!cancellation.IsCancellationRequested)
                {
                    SaveRetryError(error.Message);
                }
            }
            catch (Exception error)
            {
                SaveRetryError(error.Message);
            }
            finally
            {
                lock (_stateLock)
                {
                    _transferCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private TransferFile? FindNextFile()
        {
            lock (_stateLock)
            {
                foreach (TransferFile file in _status.Files)
                {
                    if (file.Action != "Atla" && file.Phase != "Tamamlandı")
                    {
                        return file;
                    }
                }
                return null;
            }
        }

        private async Task TransferFileAsync(TransferFile file, CancellationToken token)
        {
            string destinationPath = _folderService.GetDestinationPath(file.Name);

            // Dosya hedefe taşındıktan hemen sonra API kapandıysa tekrar indirmeyelim.
            if (_folderService.FileMatches(destinationPath, file.Size, file.Sha256))
            {
                MarkFileCompleted(file);
                return;
            }

            string partialPath = _folderService.GetPartialPath(file.Name, file.Sha256);
            long downloadedBytes = 0;
            if (File.Exists(partialPath))
            {
                downloadedBytes = new FileInfo(partialPath).Length;
            }

            if (downloadedBytes > file.Size)
            {
                File.Delete(partialPath);
                downloadedBytes = 0;
            }

            UpdateProgress(file, downloadedBytes);
            if (downloadedBytes < file.Size)
            {
                await DownloadRemainingBytesAsync(file, partialPath, downloadedBytes, token);
            }

            // 4. Önce doğrula; sonra hedef dosyayı değiştir.
            await VerifyDownloadedFileAsync(file, partialPath, token);

            lock (_stateLock)
            {
                token.ThrowIfCancellationRequested();
                File.Move(partialPath, destinationPath, true);
                MarkFileCompleted(file);
            }
        }

        private async Task DownloadRemainingBytesAsync(TransferFile file, string partialPath,
            long downloadedBytes, CancellationToken token)
        {
            string encodedFileName = Uri.EscapeDataString(file.Name);
            string downloadUrl = _sourceBaseUrl.TrimEnd('/') + "/api/source/files/" + encodedFileName;

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
            {
                if (downloadedBytes > 0)
                {
                    // Örnek: 200.000 byte inmişse yalnızca sonrası istenir.
                    request.Headers.Range = new RangeHeaderValue(downloadedBytes, null);
                    EntityTagHeaderValue fileVersion = new EntityTagHeaderValue("\"" + file.Sha256 + "\"");
                    request.Headers.IfRange = new RangeConditionHeaderValue(fileVersion);
                }

                using (HttpResponseMessage response = await _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, token))
                {
                    long startPosition = CheckServerResponse(response, file, downloadedBytes);
                    await SaveResponseToPartialFileAsync(response, file, partialPath, startPosition, token);
                }
            }
        }

        private long CheckServerResponse(HttpResponseMessage response, TransferFile file, long downloadedBytes)
        {
            response.EnsureSuccessStatusCode();
            string expectedVersion = "\"" + file.Sha256 + "\"";
            if (response.Headers.ETag == null || response.Headers.ETag.Tag != expectedVersion)
            {
                throw new IOException("Kaynak dosya değişti; duraklatıp yeniden analiz edin.");
            }

            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                ContentRangeHeaderValue? range = response.Content.Headers.ContentRange;
                if (range == null || range.From != downloadedBytes || range.Length != file.Size)
                {
                    throw new IOException("Geçersiz Content-Range.");
                }
                return downloadedBytes;
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                // Sunucu tam dosya gönderdiyse, eski kısmi dosyanın üzerine baştan yaz.
                return 0;
            }

            throw new IOException("Beklenmeyen HTTP yanıtı.");
        }

        private async Task SaveResponseToPartialFileAsync(HttpResponseMessage response,
            TransferFile file, string partialPath, long position, CancellationToken token)
        {
            FileMode fileMode = FileMode.Create;
            if (position > 0)
            {
                fileMode = FileMode.Append;
            }

            using (FileStream output = new FileStream(partialPath, fileMode, FileAccess.Write, FileShare.Read))
            using (Stream input = await response.Content.ReadAsStreamAsync(token))
            {
                byte[] buffer = new byte[64 * 1024];
                int bytesRead;

                while ((bytesRead = await input.ReadAsync(buffer, token)) > 0)
                {
                    if (position + bytesRead > file.Size)
                    {
                        throw new IOException("Dosya beklenenden büyük.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    output.Flush(true);
                    position += bytesRead;
                    UpdateProgress(file, position);

                    int delay;
                    lock (_stateLock)
                    {
                        delay = _status.DelayMilliseconds;
                    }
                    await Task.Delay(delay, token);
                }
            }
        }

        private async Task VerifyDownloadedFileAsync(TransferFile file, string partialPath, CancellationToken token)
        {
            if (new FileInfo(partialPath).Length != file.Size)
            {
                throw new IOException("Dosya eksik: " + file.Name);
            }

            lock (_stateLock)
            {
                token.ThrowIfCancellationRequested();
                _status.Phase = "Doğrulanıyor";
                SaveStatus();
            }

            string actualHash;
            using (FileStream stream = File.OpenRead(partialPath))
            {
                byte[] hash = await SHA256.HashDataAsync(stream, token);
                actualHash = Convert.ToHexString(hash);
            }

            if (actualHash != file.Sha256)
            {
                File.Delete(partialPath);
                throw new IOException("SHA-256 uyuşmazlığı: " + file.Name + "; yeniden indirilecek.");
            }
        }

        private void UpdateProgress(TransferFile file, long downloadedBytes)
        {
            lock (_stateLock)
            {
                file.Bytes = downloadedBytes;
                file.Phase = "Aktarılıyor";
                if (_isTransferRequested)
                {
                    _status.Phase = "Aktarılıyor";
                    _status.Error = null;
                }
                SaveStatus();
            }
        }

        private void MarkFileCompleted(TransferFile file)
        {
            lock (_stateLock)
            {
                file.Bytes = file.Size;
                file.Phase = "Tamamlandı";
                SaveStatus();
            }
        }

        private void SaveRetryError(string message)
        {
            lock (_stateLock)
            {
                if (_isTransferRequested)
                {
                    _status.Phase = "Yeniden deneniyor";
                    _status.Error = message;
                    SaveStatus();
                }
            }
        }

        private void EnsureTransferIsIdle()
        {
            if (_isTransferRequested || _transferCancellation != null)
            {
                throw new InvalidOperationException("Önce aktarımı duraklatın ve mevcut işlemin bitmesini bekleyin.");
            }
        }

        private void LoadSavedStatus()
        {
            if (!File.Exists(_folderService.StatePath))
            {
                return;
            }

            string json = File.ReadAllText(_folderService.StatePath);
            TransferStatus? savedStatus = JsonSerializer.Deserialize<TransferStatus>(json);
            if (savedStatus != null)
            {
                _status = savedStatus;
            }
        }

        private void SaveStatus()
        {
            // Önce geçici dosyaya yaz, sonra asıl kayıtla değiştir.
            string temporaryPath = _folderService.StatePath + ".tmp";
            string json = JsonSerializer.Serialize(_status);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _folderService.StatePath, true);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
