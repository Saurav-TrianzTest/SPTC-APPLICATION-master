using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon;
using Newtonsoft.Json;
using System;

namespace SPTC_APPLICATION.Database
{
    public class DatabaseConnection
    {
        private static string connectionString;
        private static IAmazonSecretsManager _secretsManagerClient;

        private static IAmazonSecretsManager GetSecretsManagerClient()
        {
            if (_secretsManagerClient == null)
            {
                var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
                _secretsManagerClient = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
            }
            return _secretsManagerClient;
        }

        public DatabaseConnection(string connectionString)
        {
            DatabaseConnection.connectionString = connectionString;
        }

        public static MySqlConnection GetConnection()
        {
            MySqlConnection connection = new MySqlConnection(DatabaseConnection.connectionString);
            return connection;
        }

        public static string GetEnumDescription(ConnectionLogs value)
        {
            FieldInfo fieldInfo = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

            return attributes.Length > 0 ? attributes[0].Description : value.ToString();
        }

        public class Builder
        {
            private string connectionString;

            public ConnectionLogs Log { private set; get; }

            public Builder(string host, string port, string database, string username, string password)
            {
                //connectionString = $"Server={host};Port={port};Database={database};Uid={username};Pwd={password};";
                connectionString = $"Server={host};Database={database};Uid={username};Pwd={password};";
            }

            // New constructor that retrieves connection string from AWS Secrets Manager
            public Builder(string secretName)
            {
                try
                {
                    connectionString = GetConnectionStringFromSecretsManager(secretName).Result;
                }
                catch (Exception ex)
                {
                    Log = ConnectionLogs.EXCEPTION_OCCURED;
                    throw new Exception($"Failed to retrieve connection string from Secrets Manager: {ex.Message}", ex);
                }
            }

            private async Task<string> GetConnectionStringFromSecretsManager(string secretName)
            {
                try
                {
                    var client = GetSecretsManagerClient();
                    var request = new GetSecretValueRequest
                    {
                        SecretId = secretName
                    };

                    var response = await client.GetSecretValueAsync(request);
                    
                    if (response.SecretString != null)
                    {
                        // Parse the secret JSON to build connection string
                        dynamic secret = JsonConvert.DeserializeObject(response.SecretString);
                        string host = secret.host;
                        string database = secret.database;
                        string username = secret.username;
                        string password = secret.password;
                        
                        return $"Server={host};Database={database};Uid={username};Pwd={password};";
                    }
                    else
                    {
                        throw new Exception("Secret string is null");
                    }
                }
                catch (ResourceNotFoundException)
                {
                    throw new Exception($"Secret '{secretName}' not found in AWS Secrets Manager");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error retrieving secret: {ex.Message}", ex);
                }
            }

            public async Task<bool> CreateAsync()
            {
                if (!string.IsNullOrEmpty(connectionString))
                {
                    try
                    {
                        DatabaseConnection connection = new DatabaseConnection(connectionString);

                        MySqlConnection mySqlConnection = DatabaseConnection.GetConnection();
                        await mySqlConnection.OpenAsync();
                        await Task.Delay(1000);
                        mySqlConnection.Close();

                        Log = ConnectionLogs.ESTABLISHED;
                        return true;
                    }
                    catch (MySqlException ex)
                    {
                        if (ex.Number == 1045)
                        {
                            Log = ConnectionLogs.WRONG_PASSWORD;
                        }
                        else if (ex.Number == 1042)
                        {
                            Log = ConnectionLogs.CANNOT_CONNECT;
                        }
                        else
                        {
                            Log = ConnectionLogs.EXCEPTION_OCCURED;
                        }
                        return false;
                    }
                }
                else
                {
                    Log = ConnectionLogs.STRING_EMPTY;
                    return false;
                }
            }

            public bool Connect()
            {
                if (!string.IsNullOrEmpty(connectionString))
                {
                    try
                    {
                        DatabaseConnection connection = new DatabaseConnection(connectionString);

                        MySqlConnection mySqlConnection = DatabaseConnection.GetConnection();
                        mySqlConnection.Open();
                        mySqlConnection.Close();

                        Log = ConnectionLogs.ESTABLISHED;
                        return true;
                    }
                    catch (MySqlException ex)
                    {
                        if (ex.Number == 1045)
                        {
                            Log = ConnectionLogs.WRONG_PASSWORD;
                        }
                        else if (ex.Number == 1042)
                        {
                            Log = ConnectionLogs.CANNOT_CONNECT;
                        }
                        else
                        {
                            Log = ConnectionLogs.EXCEPTION_OCCURED;
                        }
                        return false;
                    }
                }
                else
                {
                    Log = ConnectionLogs.STRING_EMPTY;
                    return false;
                }
            }
        }
    }

    public enum ConnectionLogs
    {
        [Description("Empty Connection string")]
        STRING_EMPTY,

        [Description("Connection Established")]
        ESTABLISHED,

        [Description("Exception Occurred")]
        EXCEPTION_OCCURED,

        [Description("Wrong Password")]
        WRONG_PASSWORD,

        [Description("Cannot Connect")]
        CANNOT_CONNECT,
    }
}
