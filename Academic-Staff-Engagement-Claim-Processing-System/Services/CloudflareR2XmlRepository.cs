using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.DataProtection.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class CloudflareR2XmlRepository : IXmlRepository
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public CloudflareR2XmlRepository(
            IAmazonS3 s3Client,
            string bucketName)
        {
            _s3Client = s3Client;
            _bucketName = bucketName;
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            var elements = new List<XElement>();

            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName
            };

            var response = _s3Client
                .ListObjectsV2Async(listRequest)
                .GetAwaiter()
                .GetResult();

            foreach (var obj in response.S3Objects)
            {
                // Only process XML files.
                if (!obj.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = obj.Key
                };

                using var getResponse = _s3Client
                    .GetObjectAsync(getRequest)
                    .GetAwaiter()
                    .GetResult();

                using var reader = new StreamReader(getResponse.ResponseStream);

                var xml = reader.ReadToEnd();

                if (!string.IsNullOrWhiteSpace(xml))
                {
                    elements.Add(XElement.Parse(xml));
                }
            }

            return elements;
        }

        public void StoreElement(
            XElement element,
            string friendlyName)
        {
            var keyName = friendlyName.EndsWith(
                ".xml",
                StringComparison.OrdinalIgnoreCase)
                ? friendlyName
                : $"{friendlyName}.xml";

            var xmlContent = element.ToString(
                SaveOptions.DisableFormatting);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = keyName,
                ContentBody = xmlContent,
                ContentType = "application/xml",

                // Required for Cloudflare R2.
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,

                // Prevent AWS SDK streaming/chunked upload.
                UseChunkEncoding = false
            };

            _s3Client
                .PutObjectAsync(request)
                .GetAwaiter()
                .GetResult();
        }
    }
}