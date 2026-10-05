using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages
{
    public class SignatureImageModel : PageModel
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public SignatureImageModel(
            IAmazonS3 s3Client,
            IConfiguration configuration)
        {
            _s3Client = s3Client;

            _bucketName =
                configuration["R2:BucketName"]
                ?? throw new InvalidOperationException(
                    "R2:BucketName is not configured.");
        }

        public async Task<IActionResult> OnGetAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return NotFound();
            }

            path = path.Trim();

            if (!path.StartsWith(
                    "signatures/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = path
                };

                using var response =
                    await _s3Client.GetObjectAsync(request);

                var memoryStream = new MemoryStream();

                await response.ResponseStream.CopyToAsync(
                    memoryStream);

                memoryStream.Position = 0;

                return File(
                    memoryStream,
                    "image/png");
            }
            catch (AmazonS3Exception ex)
                when (ex.StatusCode ==
                      System.Net.HttpStatusCode.NotFound)
            {
                return NotFound();
            }
        }
    }
}