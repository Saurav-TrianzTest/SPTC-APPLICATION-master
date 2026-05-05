using SPTC_APPLICATION.Database;
using System.Windows;
using SPTC_APPLICATION.Objects;
using SPTC_APPLICATION.View.Pages;
using SPTC_APPLICATION.View;
using System.Windows.Documents;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System;
using Amazon.S3;
using Amazon.S3.Model;
using System.Threading.Tasks;
using Amazon;
using System.Text;

namespace SPTC_APPLICATION
{
    public static class AppState
    {
        //SAVED EXTERNALLY - Now using AWS S3
        private static readonly string S3_BUCKET_NAME = Environment.GetEnvironmentVariable("AWS_S3_BUCKET_NAME") ?? "sptc-app-config";
        private static readonly string S3_APPSTATE_KEY = "Config/AppState.json";
        
        public static string APPSTATE_PATH = "Config\\AppState.json"; // Legacy path reference
        public static string DEFAULT_PASSWORD = Environment.GetEnvironmentVariable("DEFAULT_PASSWORD") ?? "Admin1234";
        public static string DEFAULT_ADDRESSLINE2 = "Sapang Palay San Jose Del Monte, Bulacan";
        public static string EXPIRATION_DATE = "2023 - 2024";
        public static string CHAIRMAN = "ROLLY M. LABINDAO";
        public static string REGISTRATION_NO = "9520-03006397";
        public static double PRINT_AJUSTMENTS = 24.67712;

        //NOT SAVED EXTERNALLY - Replaced static collection with configuration
        public static List<string> Employees = new List<string> { "General Manager", "Secretary", "Treasurer", "Book Keeper" };
        public static bool IS_ADMIN = false;
        public static Employee USER = null;

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

        public static void Login(string username, string password, Window window)
        {
            dynamic result = Retrieve.Login(username, password);

            if (result is View.ControlWindow controlWindow)
            {
                EventLogger.Post($"User :: Login Failed: USER({username})");
                //DEBUG THIS ON OTHER PC
                //CreateEmployee(AppState.Employees.IndexOf(username)); //result in password :: 751cb3f4aa17c36186f4856c8982bf27
            }
            else if (result is Employee employee)
            {

                USER = employee;
                (new PrintPreview()).Show();
                //(new Test()).Show();
                //(new MainBody()).Show();
                EventLogger.Post($"User :: Login Success: USER({username})");
                window.Close();
            }
        }

        public static void Logout(Window window)
        {
            IS_ADMIN = false;
            USER = null;
            EventLogger.Post($"User :: Logout Success");
            (new Login()).Show();
            window.Close();
        }

        public static async Task SaveToJsonAsync()
        {
            var data = new
            {
                APPSTATE_PATH,
                DEFAULT_PASSWORD,
                DEFAULT_ADDRESSLINE2,
                EXPIRATION_DATE,
                CHAIRMAN,
                REGISTRATION_NO,
                PRINT_AJUSTMENTS
            };

            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                var s3Client = GetS3Client();

                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = S3_BUCKET_NAME,
                        Key = S3_APPSTATE_KEY,
                        InputStream = memoryStream,
                        ContentType = "application/json"
                    };

                    await s3Client.PutObjectAsync(putRequest);
                }
            }
            catch (AmazonS3Exception ex)
            {
                ControlWindow.ShowDialog("Error saving to S3", $"S3 Error: {ex.Message}");
                EventLogger.Post($"ERR :: S3 Exception : {ex.Message}");
            }
            catch (Exception ex)
            {
                ControlWindow.ShowDialog("Error saving configuration", ex.Message);
                EventLogger.Post($"ERR :: Exception : {ex.Message}");
            }
        }

        public static void SaveToJson()
        {
            // Synchronous wrapper for backward compatibility
            Task.Run(async () => await SaveToJsonAsync()).Wait();
        }

        public static async Task LoadFromJsonAsync()
        {
            try
            {
                var s3Client = GetS3Client();

                var getRequest = new GetObjectRequest
                {
                    BucketName = S3_BUCKET_NAME,
                    Key = S3_APPSTATE_KEY
                };

                using (var response = await s3Client.GetObjectAsync(getRequest))
                using (var reader = new StreamReader(response.ResponseStream))
                {
                    string json = await reader.ReadToEndAsync();
                    dynamic data = JsonConvert.DeserializeObject(json);
                    APPSTATE_PATH = data.APPSTATE_PATH;
                    DEFAULT_PASSWORD = data.DEFAULT_PASSWORD;
                    DEFAULT_ADDRESSLINE2 = data.DEFAULT_ADDRESSLINE2;
                    EXPIRATION_DATE = data.EXPIRATION_DATE;
                    CHAIRMAN = data.CHAIRMAN;
                    REGISTRATION_NO = data.REGISTRATION_NO;
                    PRINT_AJUSTMENTS = data.PRINT_AJUSTMENTS;
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                EventLogger.Post("INFO :: AppState configuration not found in S3, using defaults");
            }
            catch (AmazonS3Exception ex)
            {
                EventLogger.Post($"ERR :: S3 Exception : {ex.Message}");
            }
            catch (Exception e)
            {
                EventLogger.Post("ERR :: Exception : " + e.Message);
            }
        }

        public static void LoadFromJson()
        {
            // Synchronous wrapper for backward compatibility
            Task.Run(async () => await LoadFromJsonAsync()).Wait();
        }
    }
}
