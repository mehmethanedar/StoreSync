namespace StoreSync.UpdateApi.Models
{
    // Ana sayfadaki sayaçlar ve dosya listesi bu modelden gelir.
    public class TransferStatus
    {
        public string Phase { get; set; } = "Hazır";
        public string? Error { get; set; }
        public int SourceFileCount { get; set; }
        public int DestinationFileCount { get; set; }
        public int DelayMilliseconds { get; set; } = 150;
        public List<TransferFile> Files { get; set; } = new List<TransferFile>();

        public int SkippedFiles
        {
            get
            {
                int count = 0;
                foreach (TransferFile file in Files)
                {
                    if (file.Action == "Atla")
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int PlannedFiles
        {
            get
            {
                return Files.Count - SkippedFiles;
            }
        }

        public int CompletedFiles
        {
            get
            {
                int count = 0;
                foreach (TransferFile file in Files)
                {
                    if (file.Action != "Atla" && file.Phase == "Tamamlandı")
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int RemainingFiles
        {
            get
            {
                return PlannedFiles - CompletedFiles;
            }
        }

        public long TotalBytes
        {
            get
            {
                long total = 0;
                foreach (TransferFile file in Files)
                {
                    if (file.Action != "Atla")
                    {
                        total += file.Size;
                    }
                }
                return total;
            }
        }

        public long TransferredBytes
        {
            get
            {
                long total = 0;
                foreach (TransferFile file in Files)
                {
                    if (file.Action != "Atla")
                    {
                        total += file.Bytes;
                    }
                }
                return total;
            }
        }

        // Web'e bir kopya göndeririz. Böylece arka plan işlemi asıl listeyi değiştirebilir.
        public TransferStatus Copy()
        {
            TransferStatus copy = new TransferStatus();
            copy.Phase = Phase;
            copy.Error = Error;
            copy.SourceFileCount = SourceFileCount;
            copy.DestinationFileCount = DestinationFileCount;
            copy.DelayMilliseconds = DelayMilliseconds;

            foreach (TransferFile file in Files)
            {
                copy.Files.Add(file.Copy());
            }
            return copy;
        }
    }
}
