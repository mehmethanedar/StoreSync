namespace StoreSync.UpdateApi.Services
{
    // Bu sınıf aynı API içinde çalışır. Ayrı uygulama açılmaz.
    public class TransferWorker : BackgroundService
    {
        private readonly TransferService _transferService;

        public TransferWorker(TransferService transferService)
        {
            _transferService = transferService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Kullanıcı Başlat'a bastıysa dosyaları aktarır; basmadıysa hemen döner.
                await _transferService.ProcessAsync(stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
