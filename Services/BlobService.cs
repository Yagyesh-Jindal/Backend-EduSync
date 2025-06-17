using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EduSyncAPI.Interfaces;
using System.IO;

namespace EduSyncAPI.Services
{
    public class BlobService : IBlobService
    {
        private readonly IConfiguration _config;
        private readonly string _connectionString;

        public BlobService(IConfiguration config)
        {
            _config = config;
            _connectionString = _config["AzureBlob:ConnectionString"];
        }

        public async Task<string> UploadFileAsync(IFormFile file, string containerName)
        {
            try
            {
                var containerClient = new BlobContainerClient(_connectionString, containerName);

                // Create the container if it doesn't exist with public access
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                // Generate a unique file name with original file name to make it more identifiable
                string fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}-{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var blobClient = containerClient.GetBlobClient(fileName);

                using (var stream = file.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream, new BlobHttpHeaders
                    {
                        ContentType = file.ContentType
                    });
                }

                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file to blob storage: {ex.Message}");
                throw;
            }
        }

        public async Task<Stream> DownloadFileAsync(string blobUrl)
        {
            try
            {
                // Extract container name and blob name from the URL
                var uri = new Uri(blobUrl);
                string path = uri.AbsolutePath;
                
                // The path format is typically: /containername/blobname
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2)
                {
                    throw new ArgumentException("Invalid blob URL format");
                }

                string containerName = segments[0];
                string blobName = string.Join("/", segments.Skip(1));

                // Create blob client
                var containerClient = new BlobContainerClient(_connectionString, containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Check if blob exists
                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"File {blobName} not found in container {containerName}");
                }

                // Download to memory stream
                var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);
                memoryStream.Position = 0; // Reset position to beginning of stream
                
                return memoryStream;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file from blob storage: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string blobUrl)
        {
            try
            {
                // Extract container name and blob name from the URL
                var uri = new Uri(blobUrl);
                string path = uri.AbsolutePath;
                
                // The path format is typically: /containername/blobname
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2)
                {
                    throw new ArgumentException("Invalid blob URL format");
                }

                string containerName = segments[0];
                string blobName = string.Join("/", segments.Skip(1));

                // Create blob client
                var containerClient = new BlobContainerClient(_connectionString, containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Delete the blob
                var response = await blobClient.DeleteIfExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting file from blob storage: {ex.Message}");
                throw;
            }
        }
    }
}
