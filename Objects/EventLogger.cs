using System;
using System.IO;
using System.Linq;
using SPTC_APPLICATION.View;
using Amazon.S3;
using Amazon.S3.Model;
using System.Threading.Tasks;
using Amazon;
using System.Text;

namespace SPTC_APPLICATION.Objects
{
    public class EventLogger
    {
        private static readonly string S3_BUCKET_NAME = Environment.GetEnvironmentVariable("AWS_S3_BUCKET_NAME") ?? "sptc-app-logs";
        private static readonly string S3_LOG_KEY = "Logs/log.txt";
        private static readonly string LogFilePath = "Logs\\log.txt"; // Legacy path reference
        private static readonly int MaxLines = 10000;

        private static IAmazonS3 _s3Client;

        private static IAmazonS3 GetS3Client()
        {
            if (_s3Client == null)
            {
                var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
                _s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
            }
            return _s3Client;
        }

        public static void Post(string message)
        {
            Task.Run(async () => await PostAsync(message)).Wait();
        }

        public static async Task PostAsync(string message)
        {
            // Use UTC time for cloud consistency
            string logEntry = $"{DateTimeOffset.UtcNow:ddd MMM-dd HH:mm} UTC :: {message}{Environment.NewLine}";

            try
            {
                var s3Client = GetS3Client();
                string currentLogContents = "";

                // Try to read existing log from S3
                try
                {
                    var getRequest = new GetObjectRequest
                    {
                        BucketName = S3_BUCKET_NAME,
                        Key = S3_LOG_KEY
                    };

                    using (var response = await s3Client.GetObjectAsync(getRequest))
                    using (var reader = new StreamReader(response.ResponseStream))
                    {
                        currentLogContents = await reader.ReadToEndAsync();
                    }
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Log file doesn't exist yet, that's okay
                    currentLogContents = "";
                }

                string updatedLogContents = logEntry + currentLogContents;

                if (updatedLogContents.CountLines() > MaxLines)
                {
                    string[] lines = updatedLogContents.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    updatedLogContents = string.Join(Environment.NewLine, lines.Take(MaxLines));
                }

                // Write updated log back to S3
                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(updatedLogContents)))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = S3_BUCKET_NAME,
                        Key = S3_LOG_KEY,
                        InputStream = memoryStream,
                        ContentType = "text/plain"
                    };

                    await s3Client.PutObjectAsync(putRequest);
                }
            }
            catch (AmazonS3Exception ex)
            {
                // Fallback to console logging if S3 fails
                Console.WriteLine($"S3 Error writing to log: {ex.Message}");
                Console.WriteLine(logEntry);
            }
            catch (Exception ex)
            {
                // Fallback to console logging if any error occurs
                Console.WriteLine($"Error writing to log: {ex.Message}");
                Console.WriteLine(logEntry);
            }
        }
    }

    public static class StringExtensions
    {
        public static int CountLines(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 1;
            int position = 0;
            while ((position = text.IndexOf(Environment.NewLine, position)) != -1)
            {
                count++;
                position += Environment.NewLine.Length;
            }

            return count;
        }
    }
}
