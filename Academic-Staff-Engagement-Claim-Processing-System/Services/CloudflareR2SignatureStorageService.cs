using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class CloudflareR2SignatureStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public CloudflareR2SignatureStorageService(
            IAmazonS3 s3Client,
            IConfiguration configuration)
        {
            _s3Client = s3Client;

            _bucketName =
                configuration["R2:BucketName"]
                ?? throw new InvalidOperationException(
                    "R2:BucketName is not configured.");
        }

        public async Task<string> SaveAsync(
            string fileName,
            byte[] content,
            string contentType = "image/png")
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException(
                    "File name is required.",
                    nameof(fileName));

            if (content == null || content.Length == 0)
                throw new ArgumentException(
                    "Signature content is empty.",
                    nameof(content));

            string safeFileName = Path.GetFileName(fileName);

            string objectKey = $"signatures/{safeFileName}";

            using var stream = new MemoryStream(content);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                UseChunkEncoding = false
            };

            await _s3Client.PutObjectAsync(request);

            return objectKey;
        }

        public async Task<bool> ExistsAsync(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
                return false;

            try
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
                };

                await _s3Client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception ex)
                when (ex.StatusCode ==
                      System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<byte[]> ReadAsync(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
                throw new ArgumentException(
                    "R2 object key is required.",
                    nameof(objectKey));

            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            using var response =
                await _s3Client.GetObjectAsync(request);

            using var memoryStream = new MemoryStream();

            await response.ResponseStream.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }

        public async Task DeleteAsync(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
                return;

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(request);
        }
    }
}