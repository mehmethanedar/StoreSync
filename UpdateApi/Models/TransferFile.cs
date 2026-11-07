namespace StoreSync.UpdateApi.Models
{
    // Listede gösterdiğimiz bir dosyanın bilgileri.
    public class TransferFile
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Phase { get; set; } = "Bekliyor";
        public long Bytes { get; set; }

        public TransferFile Copy()
        {
            TransferFile copy = new TransferFile();
            copy.Name = Name;
            copy.Size = Size;
            copy.Sha256 = Sha256;
            copy.Action = Action;
            copy.Phase = Phase;
            copy.Bytes = Bytes;
            return copy;
        }
    }
}
