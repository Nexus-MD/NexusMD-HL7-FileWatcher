using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceProcess;

namespace FileWatcher_WindowsService
{
    public partial class Service1 : ServiceBase
    {
        ConfigJsonModel configJson;
        string apiPath = "https://api.nexus-md.com/v1/file_manager/hl7_messages";
        private FileSystemWatcher fileSystemWatcher;
        public Service1()
        {
            InitializeComponent(); 
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                string logSource = "File Watcher Windows Service";
                string logName = "FileWatcherLogs";

                eventLog1 = new System.Diagnostics.EventLog();
                if (!System.Diagnostics.EventLog.SourceExists(logSource))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        logSource, logName);
                }
                eventLog1.Source = logSource;
                eventLog1.Log = logName;

                ReadConfig();


                fileSystemWatcher = new FileSystemWatcher(configJson.PathToWatch);
                fileSystemWatcher.EnableRaisingEvents = true;
                fileSystemWatcher.Created += new FileSystemEventHandler(FileCreated);
                eventLog1.WriteEntry("In OnStart.");
            }
            catch (Exception)
            {
            }
           
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("In Stop.");
        }
        private void ReadConfig()
        {
            string log = "Read Config";
            string filePath = "c:\\fileWatcherConfig.json";

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    log += $"\nConfig is exist.";
                    using (StreamReader r = new StreamReader(filePath))
                    {
                        string json = r.ReadToEnd();
                        configJson = JsonConvert.DeserializeObject<ConfigJsonModel>(json);
                    }
                }
                else
                {
                   

                    log += $"\nConfig dose not exist. new config file created at {filePath}.";
                    configJson = new ConfigJsonModel()
                    {
                        PathToWatch = "C:\\AmirTest",
                        KeepOriginalFile = true
                    };
                    using (StreamWriter writer = new StreamWriter(filePath, true))
                    {
                        {
                            string output = JsonConvert.SerializeObject(configJson);
                            writer.Write(output);
                        }
                        writer.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                log += $"\n\nExeption : {ex.Message}";
                configJson = new ConfigJsonModel()
                {
                    PathToWatch = "C:\\AmirTest",
                    KeepOriginalFile = true
                };
            }
            log += $"\nConfig is : {JsonConvert.SerializeObject(configJson)}";
            eventLog1.WriteEntry(log);
        }
        private void FileCreated(Object sender, FileSystemEventArgs e)
        {
            if (e.FullPath.Contains("\\edittemp"))
                return;
            string log = "See file with name: " + e.Name;
            try
            {
                StreamReader reader = new StreamReader(e.FullPath);
                string input = reader.ReadToEnd();
                reader.Close();
                log += $"\nFile opened";
                //log += $"\nSee this Content: {input}";

                #region Replace and save to temp
                string editedFilePath;
                if (configJson.KeepOriginalFile)
                {
                    editedFilePath = $"{configJson.PathToWatch}\\edittemp\\";
                    if (!Directory.Exists(editedFilePath))
                        Directory.CreateDirectory(editedFilePath);

                    editedFilePath += e.Name;
                }
                else
                {
                    editedFilePath = e.FullPath;
                }

                using (StreamWriter writer = new StreamWriter(editedFilePath, true))
                {
                    {
                        string output = input.Replace("MSH|^~\\&|MIK-AIG^MIK-AIG^GUID||", "MSH|^~\\&|MIK-AIG^RALEIGHORTHO^GUID||");
                        writer.Write(output);
                    }
                    writer.Close();
                }
                log += $"\nReplace Success and saved in {editedFilePath}"; 
                #endregion

                #region Send to api
                var form = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(File.ReadAllBytes(editedFilePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                form.Add(fileContent, "files", e.Name);

                var httpClient = new HttpClient();

                var response = httpClient.PostAsync(apiPath, form).Result;

                log += $"\nApi StatusCode: {response.StatusCode}";
                if (response.IsSuccessStatusCode)
                {
                    log += $"\nResponseContent: {response.Content.ReadAsStringAsync().Result}";
                }
                #endregion

                #region Delete Temp
                if (configJson.KeepOriginalFile)
                {
                    System.IO.File.Delete(editedFilePath);
                    log += $"\nThe edited file was deleted";
                }
                #endregion

                log += $"\n Finish";
            }
            catch (Exception ex)
            {
                log += $"\n\nExeption: {ex.Message}";
            }
            finally
            {
                eventLog1.WriteEntry(log);
            }
        }
       
    }
}
