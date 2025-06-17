namespace EduSyncAPI.Interfaces
{
    public interface IBlobService
    {
        Task<string> UploadFileAsync(IFormFile file, string containerName);
        Task<Stream> DownloadFileAsync(string blobUrl);
        Task<bool> DeleteFileAsync(string blobUrl);
    }
}
