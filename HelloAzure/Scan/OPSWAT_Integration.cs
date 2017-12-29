using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace HelloAzure.Scanner
{
    class OPSWAT_Integration
    {
        #region Class Properties
        static string rest_url, api_token;
        public static string REST_URL
        {
            get
            {
                return rest_url;
            }

            set
            {
                rest_url = value;
            }
        }

        public static string API_TOKEN
        {
            get
            {
                return api_token;
            }

            set
            {
                api_token = value;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// isShouldBlock will post a file or base64 to Metadefender core server for processing.
        /// Metadefender will process the file on the specified policy rule and return the scan results.
        /// </summary>
        /// <param name="base64_input"></param>
        /// <param name="filename"></param>
        /// <param name="rule_name"></param>
        /// <param name="timeout"></param>
        /// <param name="results_details"></param>
        /// <param name="sanitized_file"></param>
        /// <returns>true - the file should be blocked by the policy rule. false - the file is OK and should not be blocked by the policy rule</returns>
        public static bool isShouldBlock(string base64_input, string filename, string rule_name, int timeout, out string results_details, out MemoryStream sanitized_file)
        {
            // init objects
            results_details = "Failed to scan";
            sanitized_file = null;
            string data_id = null;

            // init and request a file scan
            data_id = InitFileScan(base64_input, filename, timeout, string.Empty, api_token, string.Empty, rule_name);

            // let the scan begin
            System.Threading.Thread.Sleep(150);

            // create an object for the response
            OPSWAT_MD_OBJECT omd_obj = new OPSWAT_MD_OBJECT();

            // get the scan results by the data_id
            if (string.IsNullOrEmpty(data_id))
                throw new Exception("No data_id provided");

            // to supporting large json responses (extracted files inside archive)
            jsonSerializer.MaxJsonLength = int.MaxValue;

            // init progress precentage
            int i_progress = 0;

            // request for scan results until progress precentage reached 100
            while (i_progress < 100)
            {
                // request for scan results
                omd_obj = GetResultsByDataID(data_id, timeout);

                if (omd_obj.process_info != null)
                {
                    i_progress = omd_obj.process_info.progress_percentage;
                }
                else
                {
                    throw new Exception("Failed to get scan results from server. Process info is null");
                }

                // wait a bit before requesting again the scan results
                if (i_progress < 100)
                    System.Threading.Thread.Sleep(150);
            }

            // check if the file being sanitized (reconstructed) or file type converted
            if (omd_obj.process_info.post_processing != null)
            {
                if (!string.IsNullOrEmpty (omd_obj.process_info.post_processing.converted_to))
                {
                    string download_error = null;

                    // download the sanitized file
                    download_error = DownloadSaniziedFile(data_id, timeout, out sanitized_file);

                    if (!string.IsNullOrEmpty(download_error))
                        throw new Exception("Failed to download sanitized file: " + download_error);
                }
            }

            // analyze the scan results
            if (omd_obj.process_info != null)
                results_details = omd_obj.process_info.blocked_reason;

            // set the function to true or false according the general results (Allowed/Blocked)
            bool bShouldBlock = true;
            if (omd_obj.process_info != null)
                bShouldBlock = omd_obj.process_info.result.ToLower() == "allowed" ? false : true;

            return bShouldBlock;
        }

        /// <summary>
        /// GetRules method will request the Metadefender server to return the active policy rules in a string array
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>String array of the server policy rules</returns>
        public static string[] GetRules(int timeout)
        {
            string[] rules_tmp = null;

            OPSWAT_MD_OBJECT_RULES[] rules_obj = null;
            rules_obj = GetScanRules(timeout);

            rules_tmp = new string[rules_obj.Length];
            for (int i = 0; i < rules_obj.Length; i++)
                rules_tmp[i] = rules_obj[i].name;

            return rules_tmp;

        }
        #endregion

        #region Private Methods
        static HttpWebRequest request = null;
        static System.Web.Script.Serialization.JavaScriptSerializer jsonSerializer =
            new System.Web.Script.Serialization.JavaScriptSerializer();

        private static string InitFileScan(string base64_input, string filepath, int timeout, string archivePwd = "", string apiKey = "", string user_agent = "", string rule = "")
        {
            // create object for returned data_id
            string data_id = null;
            string json_response = null;
            Dictionary<string, object> deserializedJsonDictionary;

            // Create request
            string requestURL = rest_url + "/file";
            request = (HttpWebRequest)WebRequest.Create(requestURL);

            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Timeout = timeout * 1000; // sec - recommneded 300sec

            // Optional headers
            if (!string.IsNullOrEmpty(apiKey))
                request.Headers.Add("apikey", apiKey);

            if (!string.IsNullOrEmpty(archivePwd))
                request.Headers.Add("archivepwd", archivePwd);

            // Mandatory headers
            if (!string.IsNullOrEmpty(user_agent))
                request.Headers.Add("user_agent", user_agent);

            if (!string.IsNullOrEmpty(rule))
                request.Headers.Add("rule", rule);

            request.Headers.Add("filename", 
                System.Web.HttpUtility.UrlPathEncode(Path.GetFileName(filepath)));

            byte[] buffer = { 0 };

            if (!string.IsNullOrEmpty (base64_input))
                // Convert Base64 to stream
                buffer = Convert.FromBase64String(base64_input);

            using (Stream rs = request.GetRequestStream())
            {
                using (FileStream fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                {
                    buffer = new byte[4096];
                    int bytesRead = 0;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        rs.Write(buffer, 0, bytesRead);
                    }
                }
            }

            buffer = null;

            // get response
            using (StreamReader response = new StreamReader(request.GetResponse().GetResponseStream()))
                {
                    json_response = response.ReadToEnd();
                }

                // Deserialize data_id from JSON
                deserializedJsonDictionary = (Dictionary<string, object>)jsonSerializer.DeserializeObject(json_response);
                data_id = (string)deserializedJsonDictionary["data_id"];

                // clear request object
                request = null;

                // return data_id
                return data_id;
            }

        private static OPSWAT_MD_OBJECT GetResultsByDataID(string data_id, int timeout)
        {
            request = (HttpWebRequest)WebRequest.Create(rest_url + "/file/" + data_id);
            request.Method = "GET";
            request.ContentType = "application/json; charset=utf-8";
            request.Timeout = timeout * 1000; // sec recommended - 60 sec

            string jsonRes = "";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    jsonRes = reader.ReadToEnd();
                }
            }

            request = null;

            return jsonSerializer.Deserialize<OPSWAT_MD_OBJECT>(jsonRes);
        }

        private static string DownloadSaniziedFile(string data_id, int timeout, out MemoryStream outputStream)
        {
            request = (HttpWebRequest)WebRequest.Create(rest_url + "/file/converted/" + data_id);
            request.Method = "GET";
            request.ContentType = "application/json; charset=utf-8";
            request.Timeout = timeout * 1000; // sec; recommended - 60 sec

            string json_response = null;
            string error = null;
            Dictionary<string, object> deserializedJsonDictionary;

            outputStream = null;

            using (var response = request.GetResponse())
            {
                if (response.ContentType.ToLower() != "application/json; charset=utf-8")
                {
                    using (var fileStream = response.GetResponseStream())
                    {
                        if (fileStream == null)
                        {
                            request = null;
                            outputStream = null;

                            return "no stream found!";
                        }

                        outputStream = new System.IO.MemoryStream();
                        fileStream.CopyTo(outputStream);
                    }
                }
                else
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        json_response = reader.ReadToEnd();
                    }

                    // Deserialize data_id from JSON
                    deserializedJsonDictionary = (Dictionary<string, object>)jsonSerializer.DeserializeObject(json_response);
                    error = (string)deserializedJsonDictionary["err"];
                }
            }

            request = null;
            return error;
        }

        private static OPSWAT_MD_OBJECT_RULES[] GetScanRules (int timeout)
        {
            request = (HttpWebRequest)WebRequest.Create(rest_url + "/file/rules");
            request.Method = "GET";
            request.ContentType = "application/json; charset=utf-8";
            request.Timeout = timeout * 1000; // sec - recommended 30 sec

            string jsonRes = "";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    jsonRes = reader.ReadToEnd();
                }
            }

            request = null;

            return jsonSerializer.Deserialize<OPSWAT_MD_OBJECT_RULES[]>(jsonRes);
        }
        #endregion
    }

    #region OPSWAT Object classes
    public class OPSWAT_MD_OBJECT
    {
        public string file_id { get; set; }
        public OPSWAT_MD_OBJECT_SCANRESULTS scan_results { get; set; }
        public OPSWAT_MD_OBJECT_FILEINFO file_info { get; set; }
        public OPSWAT_MD_OBJECT_ORIGINALFILE original_file { get; set; }
        public string data_id { get; set; }
        public int rescan_count { get; set; }
        public int share_file { get; set; }
        public string source { get; set; }
        public OPSWAT_MD_OBJECT_PROCESSINFO process_info { get; set; }
        public string scanned_on { get; set; }
        public OPSWAT_MD_OBJECT_EXTRACTEDFILES extracted_files { get; set; }
    }

    public class OPSWAT_MD_OBJECT_SCANRESULTS
    {
        public Dictionary<object, OPSWAT_MD_OBJECT_SCANDETAILS> scan_details { get; set; }
        public bool rescan_available { get; set; }
        public string data_id { get; set; }
        public int scan_all_result_i { get; set; }
        public DateTime? start_time { get; set; }
        public double total_time { get; set; }
        public int total_avs { get; set; }
        public int progress_percentage { get; set; }
        public int in_queue { get; set; }
        public string scan_all_result_a { get; set; }
    }

    public class OPSWAT_MD_OBJECT_SCANDETAILS
    {
        public string scan_result_i { get; set; }
        public string threat_found { get; set; }
        public DateTime? def_time { get; set; }
        public double scan_time { get; set; }
    }

    public class OPSWAT_MD_OBJECT_FILEINFO
    {
        public long file_size { get; set; }
        public DateTime? upload_timestamp { get; set; }
        public string md5 { get; set; }
        public string sha1 { get; set; }
        public string sha256 { get; set; }
        public string file_type_category { get; set; }
        public string file_type_description { get; set; }
        public string file_type_extension { get; set; }
        public string display_name { get; set; }
        public string full_path { get; set; }
    }

    public class OPSWAT_MD_OBJECT_PROCESSINFO
    {
        public OPSWAT_MD_OBJECT_PROCESSINFO_POSTPROCESSING post_processing { get; set; }
        public int progress_percentage { get; set; }
        public string user_agent { get; set; }
        public string profile { get; set; }
        public string result { get; set; }
        public string blocked_reason { get; set; }
        public bool file_type_skipped_scan { get; set; }
    }

    public class OPSWAT_MD_OBJECT_PROCESSINFO_POSTPROCESSING
    {
        public string actions_ran { get; set; }
        public string actions_failed { get; set; }
        public string converted_to { get; set; }
        public string copy_move_destination { get; set; }
        public string converted_destination { get; set; }
    }

    public class OPSWAT_MD_OBJECT_EXTRACTEDFILES
    {
        public string data_id { get; set; }
        public int scan_result_i { get; set; }
        public int progress_percentage { get; set; }
        public int detected_by { get; set; }
        public List<OPSWAT_MD_OBJECT_FILESINARCHIVE> files_in_archive { get; set; }
    }

    public class OPSWAT_MD_OBJECT_FILESINARCHIVE
    {
        public string display_name { get; set; }
        public string data_id { get; set; }
        public int scan_result_i { get; set; }
        public int progress_percentage { get; set; }
        public long file_size { get; set; }
        public string file_type { get; set; }
        public int detected_by { get; set; }
    }

    public class OPSWAT_MD_OBJECT_ORIGINALFILE
    {
        public string data_id { get; set; }
        public int scan_result_i { get; set; }
        public int progress_percentage { get; set; }
        public int detected_by { get; set; }
    }

    public class OPSWAT_MD_OBJECT_RULES
    {
        public string max_file_size { get; set; }
        public string name { get; set; }
    }
    #endregion
}
