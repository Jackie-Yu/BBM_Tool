/*
 * Author : Jackie Yu
 * Date : 2012-4-24
 * */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Collections.Specialized;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Achievo.Poster
{
    public partial class PostForm : Form
    {
        private long elapsedTime = 0;
        private string costFormat = "Response - Costs: {0} ms";
        private const string LINE_SEPARATE = "\n\r=======================[{0}], Costs:{1} ms, Length:{2}, traceId:{3} =====================\n\r";

        private static SharedHttpHeaderSettingEntity SHARED_HTTP_HEADER_SETTING = null;
        private static List<HostEntity> HOSTS = null;
        private DateTime LastExecuteTime = DateTime.Now;
        
        public PostForm()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
            System.Net.ServicePointManager.DefaultConnectionLimit = 5000;
        }

        private void PostForm_Load(object sender, EventArgs e)
        {
            this.txtRequestHeader.Text = @"Content-Type: application/json
appid : com.accela.inspector
//Content-Type: application/x-www-form-urlencoded
//BizURL : 
//AccessKey :
";

            LoadHistory(null);
            LoadHosts();
            //LoadSharedHttpHeaderSetting();
        }

        private void LoadHosts()
        {
            try
            {
                HOSTS = FileUtility.ReadHostsFromFile();
                BindHosts(this.cmbHosts, HOSTS);
                BindHosts(cmbHostsTab2, HOSTS, -1);
                //BindHostsToDataGrid(HOSTS);
                ShowHostsToHostsText(HOSTS);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void BindHosts(ComboBox cmbBox, List<HostEntity> hosts, int selectedIndex = 0)
        {
            cmbBox.Items.Clear();

            if(hosts != null && hosts.Count > 0)
            {
                var hostArr = hosts.ToArray();
                cmbBox.Items.AddRange(hostArr);

                if (cmbBox.Items.Count > 0)
                {
                    cmbBox.SelectedIndex = selectedIndex;
                }
            }
        }

        private void LoadHistory(string name)
        {
            try
            {
                this.cmbURL.Items.Clear();
                FileInfo[] files = FileUtility.GetHistoryList();
                if (files != null && files.Length > 0)
                {
                    foreach (var f in files)
                    {
                        string fileName = f.Name.Substring(0, f.Name.Length - 4);
                        this.cmbURL.Items.Add(fileName);
                        if (fileName.Equals(name))
                        {
                            cmbURL.SelectedItem = name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void LoadSharedHttpHeaderSetting(HostEntity host)
        {
            try
            {
                if (host == null) return;
                // open application, init a token
                //btnGenerateToken_Click(null, null);
                SHARED_HTTP_HEADER_SETTING = FileUtility.ReadHttpHeaderSettingFile(host);

                FillSharedHttpHeaderSettings(SHARED_HTTP_HEADER_SETTING);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void FillSharedHttpHeaderSettings(SharedHttpHeaderSettingEntity entity)
        {
            if(entity == null)
            {
                this.txtAgency.Clear();
                this.txtAppID.Clear();
                this.txtAppSecret.Clear();
                this.txtContentType.Clear();
                this.txtEnvironment.Clear();
                this.txtRequestBodyForGettingToken.Clear();
                this.txtRequestUrlForGettingToken.Clear();
                this.txtResponseToken.Clear();
                this.txtAccessKey.Clear();
            }
            else
            {
                this.txtAgency.Text = entity.Agency;
                this.txtAppID.Text = entity.AppId;
                this.txtAppSecret.Text = entity.AppSecret;
                this.txtContentType.Text = entity.ContentType;
                this.txtEnvironment.Text = entity.Environment;
                this.txtRequestBodyForGettingToken.Text = entity.GetTokenRequestBody;
                this.txtRequestUrlForGettingToken.Text = entity.GetTokenRequestUrl;
                this.txtResponseToken.Text = entity.AccessToken;
                this.txtAccessKey.Text = entity.AccessKey;
            }
        }

        private async void btnGet_Click(object sender, EventArgs e)
        {
            await PostPutDeletePatch(HttpMethod.Get);
        }

        private async void btnPost_Click(object sender, EventArgs e)
        {
            await PostPutDeletePatch(HttpMethod.Post);
        }

        private async void btnPut_Click(object sender, EventArgs e)
        {
            await PostPutDeletePatch(HttpMethod.Put);
        }

        private async void btnDelete_Click(object sender, EventArgs e)
        {
            await PostPutDeletePatch(HttpMethod.Delete);
        }

        private void btnPatch_Click(object sender, EventArgs e)
        {
            //PostPutDeletePatch(HttpMethod.);
        }

        private async Task PostPutDeletePatch(HttpMethod httpMethod)
        {
            try
            {
                RegenerateTokenWhenMoreThan24Hours();

                this.txtResponseHeader.Clear();
                this.txtResponse.Text = "Processing...";
                elapsedTime = 0;
                groupResponse.Text = "Response";
                string url = this.txtRequestUrl.Text.Trim();
                if (String.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show("Request URL is required.", "Warn");
                    return;
                }

                listView1.Items.Clear();
                int concurrent = Convert.ToInt32(this.numericUpDown1.Value);
  
                NameValueCollection headers = this.GetHeaders(this.chkReplaceHeader.Checked, SHARED_HTTP_HEADER_SETTING);
                string requestBody = this.txtRequestBody.Text.Trim();

                // article id 
                int idStart = Convert.ToInt32(this.txtArtIdStart.Text.Trim());
                int idEnd = Convert.ToInt32(this.txtArtIdEnd.Text.Trim());
                for (int m = idStart; m <= idEnd; m++)
                {
                    requestBody = "id=" + m.ToString();
                    TaskScheduler uiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                    Task[] tasks = new Task[concurrent];
                    for (int i = 0; i < concurrent; i++)
                    {
                        int index = i + 1;

                        tasks[i] = Task.Run(async () => await SendRequest(httpMethod, headers, index, concurrent, url, requestBody, uiTaskScheduler));
                    }
                }

                //// start task
                //await Task.WhenAll(tasks);

                //for (int i = 0; i < concurrent; i++)
                //{
                //    int index = i + 1;
                //    await SendRequest(httpMethod, headers, index, concurrent, url, requestBody);
                //}
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void btnDeleteHistory_Click(object sender, EventArgs e)
        {
            if (cmbURL.SelectedItem == null || String.IsNullOrWhiteSpace(cmbURL.SelectedItem.ToString()))
            {
                MessageBox.Show("Please select a favorite that you want to delete.");
                return;
            }

            try
            {
                var result = MessageBox.Show("Are you sure to delete '" + cmbURL.SelectedItem.ToString() + "' from favorites?", "Warn", MessageBoxButtons.OKCancel);
                if (result == DialogResult.OK)
                {
                    FileUtility.DeleteHistory(cmbURL.SelectedItem.ToString());
                    LoadHistory(null);
                    this.txtSaveAs.Clear();
                    this.txtRequestUrl.Clear();
                    this.txtRequestBody.Clear();
                    this.txtResponse.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string name = txtSaveAs.Text.Trim();
            if (String.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Name is required.", "Error");
                return;
            }
            else if (!FileUtility.IsValidFileName(name))
            {
                MessageBox.Show("File Name is invalid.", "Error");
                return;
            }

            SaveRequestData(name);

            MessageBox.Show("The request has been saved to favorites.", "Success");

            LoadHistory(name);
        }

        private async Task SendRequest(HttpMethod httpMethod, NameValueCollection headers, int sequence, int total, string requestUrl, string requestBody, TaskScheduler uiTaskScheduler)
        {
            requestUrl = requestUrl.Replace(" ", "").Replace("\\r", "").Replace("\\n", "").Replace("\\r\\n", "");

            try
            {

                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
                HttpClient client = new HttpClient();
                HttpRequestMessage requestMessage = new HttpRequestMessage(httpMethod, requestUrl);
               
               // requestMessage.Version = Version.Parse("1.0");
                if (httpMethod != HttpMethod.Get)
                {
                    requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, headers["Content-Type"]);
                }

                SetRequestHeader(client, requestMessage, headers);

                string responseString = string.Empty;

                elapsedTime = 0;
                Stopwatch watch = new Stopwatch();
                watch.Start();
                //TaskScheduler uiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
    
               
                var task = client.SendAsync(requestMessage);

                await task.ContinueWith((requestTask) =>
                {
                    watch.Stop();
                    elapsedTime = watch.ElapsedMilliseconds;
                    
                    if (this.txtResponse.Text == "Processing...")
                    {
                        this.txtResponse.Text = "Results :";
                    }

                    string traceId = GetTraceIdFromHeader(requestTask.Result.Headers);

                    // Get HTTP response from completed task.
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                        if(sequence == 1 && this.chkReplaceHeader.Checked
                            && SHARED_HTTP_HEADER_SETTING != null 
                            && !String.IsNullOrWhiteSpace(SHARED_HTTP_HEADER_SETTING.GetTokenRequestUrl)
                            && !String.IsNullOrWhiteSpace(SHARED_HTTP_HEADER_SETTING.GetTokenRequestBody)
                            && requestTask.Result.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            //auto generate a access token
                            this.GenerateToken(SHARED_HTTP_HEADER_SETTING.GetTokenRequestUrl, SHARED_HTTP_HEADER_SETTING.GetTokenRequestBody);
                        }

                        //    HttpResponseMessage response = requestTask.Result;
                        var header = requestTask.Result.Content.Headers.ToString();
                        this.txtResponseHeader.Text = ((int)requestTask.Result.StatusCode) + "-" + requestTask.Result.StatusCode.ToString() + "\r\n" + header + requestTask.Result.Headers.ToString();
                        //response.Content.Headers.ContentType
                        var resultStream = requestTask.Result.Content.ReadAsStreamAsync().Result;
                        if (requestTask.Result.Content.Headers.ContentEncoding.Count > 0)
                        {
                            var contentEncoding = requestTask.Result.Content.Headers.ContentEncoding.First();
                            resultStream = Decompress(resultStream, contentEncoding);
                            resultStream.Position = 0;
                            StreamReader reader = new StreamReader(resultStream);
                            responseString = reader.ReadToEnd();
                            resultStream.Close();
                            reader.Close();
                        }
                        else
                        {
                            responseString = requestTask.Result.Content.ReadAsStringAsync().Result;
                        }

                        this.txtResponse.Text += String.Format(LINE_SEPARATE, sequence, elapsedTime, responseString.Length, traceId) + responseString;
                        groupResponse.Text = String.Format(costFormat, elapsedTime);
                        ListViewItem item = new ListViewItem(new string[] { sequence.ToString(), elapsedTime.ToString() });
                        listView1.Items.Add(item);
                    }
                    else if (requestTask.Status == TaskStatus.Faulted)
                    {
                        this.txtResponse.Text += String.Format(LINE_SEPARATE, sequence, elapsedTime, responseString.Length, traceId);
                        this.txtResponse.Text += "Request Failed. Exception: \r\n" + GetInnerException( requestTask.Exception).Message;
                    }
                    else
                    {
                        this.txtResponse.Text += String.Format(LINE_SEPARATE, sequence, elapsedTime, responseString.Length, traceId);
                        this.txtResponse.Text += "Request Failed, please check the request url.";
                    }
                }, uiTaskScheduler);
            }
            catch (Exception ex)
            {
                this.txtResponse.Text += String.Format(LINE_SEPARATE, sequence, elapsedTime, 0, "");
                this.txtResponse.Text += GetInnerException(ex).Message;
            }
        }

        private static string GetTraceIdFromHeader(HttpResponseHeaders headers)
        {
            if(headers == null || headers.Count() == 0)
            {
                return string.Empty;
            }

            foreach(var h in headers)
            {
                if (h.Key.Equals("x-accela-traceId", StringComparison.OrdinalIgnoreCase))
                {
                    return h.Value.FirstOrDefault();
                }
            }

            return string.Empty;
        }

        private Exception GetInnerException(Exception ex)
        {
            Exception innerException = ex;

            while (innerException != null && innerException.InnerException != null)
            {
                innerException = innerException.InnerException;
            }

            return innerException;
        }
        private static Stream Compress(Stream raw)
        {
            MemoryStream memory = new MemoryStream();

            using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true))
            {
                byte[] buffer = new byte[1024];
                int nRead;
                while ((nRead = raw.Read(buffer, 0, buffer.Length)) > 0)
                {
                    gzip.Write(buffer, 0, nRead);
                }
            }
            return memory;
        }
        private static Stream Decompress(Stream raw, string contentEncoding)
        {
            MemoryStream memory = new MemoryStream();

            if ("gzip".Equals(contentEncoding, StringComparison.OrdinalIgnoreCase))
            {
                using (GZipStream gzip = new GZipStream(raw, CompressionMode.Decompress, true))
                {
                    byte[] buffer = new byte[1024];
                    int nRead;
                    while ((nRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        memory.Write(buffer, 0, nRead);
                    }
                }
            }
            else if ("deflate".Equals(contentEncoding, StringComparison.OrdinalIgnoreCase))
            {
                using (DeflateStream gzip = new DeflateStream(raw, CompressionMode.Decompress, true))
                {
                    byte[] buffer = new byte[1024];
                    int nRead;
                    while ((nRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        memory.Write(buffer, 0, nRead);
                    }
                }
            }
            else
            {
                return raw;
            }
            return memory;
        }

        private void SetRequestHeader(HttpClient httpClient, HttpRequestMessage request, NameValueCollection headers)
        {
            //The restricted headers are:
            string[] restrictedHeaders = new string[]{
                    "Accept","Connection","Content-Length",
                    "Content-Type","Date","Expect","Host",
                    "If-Modified-Since","Range","Referer",
                    "Transfer-Encoding","User-Agent",
                    "Proxy-Connection" };

            foreach (var key in headers.AllKeys)
            {
                if (key.Equals("Content-Type", StringComparison.InvariantCultureIgnoreCase))
                {
                    //httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(headers[key]));
                }
                else if (key.Equals("Accept", StringComparison.InvariantCultureIgnoreCase))
                {
                }
                else if (key.Equals("Connection", StringComparison.InvariantCultureIgnoreCase))
                {
                }
                else if (key.Equals("Content-Length", StringComparison.InvariantCultureIgnoreCase))
                {
                    // httpClient.DefaultRequestHeaders.
                }
                else if (key.Equals("Host", StringComparison.InvariantCultureIgnoreCase))
                {
                    httpClient.DefaultRequestHeaders.Host = headers[key];
                }
                else if (key.Equals("User-Agent", StringComparison.InvariantCultureIgnoreCase))
                {

                }
                else if (key.Equals("Transfer-Encoding", StringComparison.InvariantCultureIgnoreCase))
                {

                }
                else if (key.Equals("Proxy-Connection", StringComparison.InvariantCultureIgnoreCase))
                {
                }
                else if (key.Equals("If-Modified-Since", StringComparison.InvariantCultureIgnoreCase))
                {
                }
                else if (key.Equals("Range", StringComparison.InvariantCultureIgnoreCase))
                {
                }
                else if (key.Equals("Referer", StringComparison.InvariantCultureIgnoreCase))
                {

                }
                else if (key.Equals("Date", StringComparison.InvariantCultureIgnoreCase))
                {

                }
                else if (key.Equals("Expect", StringComparison.InvariantCultureIgnoreCase))
                {
                }
                else
                {
                    request.Headers.Add(key, headers[key]);
                }
            }

        }
        private NameValueCollection GetHeaders(bool usingSharedHeader, SharedHttpHeaderSettingEntity sharedSetting)
        {
            NameValueCollection headerList = new NameValueCollection();

            string headers = this.txtRequestHeader.Text.Trim();

            string[] arr = headers.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in arr)
            {
                if (!String.IsNullOrWhiteSpace(s))
                {
                    string[] kv = s.Split(new char[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (kv.Length > 1)
                    {
                        string key = kv[0].Trim().ToLower();
                        if (!key.StartsWith("//"))
                        {

                            headerList.Add(key, kv[1].Trim());
                        }
                    }
                }
            }

            if (usingSharedHeader && sharedSetting != null)
            {
                if (headerList.AllKeys.Contains("content-type"))
                {
                    headerList["content-type"] = sharedSetting.ContentType;
                }
                else
                {
                    headerList.Add("content-type", sharedSetting.ContentType);
                }

                if (headerList.AllKeys.Contains("x-accela-appid"))
                {
                    headerList["x-accela-appid"] = sharedSetting.AppId;
                }
                else
                {
                    headerList.Add("x-accela-appid", sharedSetting.AppId);
                }

                if (headerList.AllKeys.Contains("x-accela-appsecret"))
                {
                    headerList["x-accela-appsecret"] = sharedSetting.AppSecret;
                }
                else
                {
                    headerList.Add("x-accela-appsecret", sharedSetting.AppSecret);
                }

                if (headerList.AllKeys.Contains("x-accela-agency"))
                {
                    headerList["x-accela-agency"] = sharedSetting.Agency;
                }
                else
                {
                    headerList.Add("x-accela-agency", sharedSetting.Agency);
                }

                if (headerList.AllKeys.Contains("x-accela-environment"))
                {
                    headerList["x-accela-environment"] = sharedSetting.Environment;
                }
                else
                {
                    headerList.Add("x-accela-environment", sharedSetting.Environment);
                }

                if (headerList.AllKeys.Contains("authorization"))
                {
                    headerList["authorization"] = sharedSetting.AccessToken;
                }
                else
                {
                    headerList.Add("authorization", sharedSetting.AccessToken);
                }

                if (headerList.AllKeys.Contains("x-accela-subsystem-accesskey"))
                {
                    headerList["x-accela-subsystem-accesskey"] = sharedSetting.AccessKey;
                }
                else
                {
                    headerList.Add("x-accela-subsystem-accesskey", sharedSetting.AccessKey);
                }
            }
            //if (sharedSetting != null)
            //{
            //    headerList.AllKeys.Contains("Content-Type")
            //    var enumerator = headerList.GetEnumerator();

            //    while(enumerator.MoveNext())
            //    {
            //        //enumerator.Current.
            //    }
            //}

            return headerList;
        }

        private void SaveRequestData(string name)
        {
            /* file format
             ----------RequestURL----------
             RequestURL={0}
             ----------RequestURL----------
             ----------RequestHeader----------
             RequestHeader={1}
             ----------RequestHeader----------
             ----------RequestBody----------
             RequestBody={2}
             ----------RequestBody----------
             */

            string fileFormat = @"----------RequestURL----------
RequestURL={0}
----------RequestURL----------
----------RequestHeader----------
RequestHeader={1}
----------RequestHeader----------
----------RequestBody----------
RequestBody={2}
----------RequestBody----------";

            string requestUrl = this.txtRequestUrl.Text.Trim();
            string requestHeader = this.txtRequestHeader.Text.Trim();
            string requestBody = this.txtRequestBody.Text.Trim();
            string content = String.Format(fileFormat, requestUrl, requestHeader, requestBody);
            try
            {
                FileUtility.WriteFile(name + ".txt", content);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
                return;
            }
        }

        private void cmbURL_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                groupResponse.Text = "Response";
                string selected = cmbURL.Text as string;
                if (string.IsNullOrEmpty(selected))
                {
                    return;
                }

                RequestEntity re = FileUtility.GetRequestDataFromFile(selected);
                if (re != null)
                {
                    // get host
                    string host = "";
                    var selectHost = this.cmbHosts.SelectedItem as HostEntity;
                    if (selectHost != null)
                    {
                        host = selectHost.HostUrl;
                    }

                    if (!String.IsNullOrWhiteSpace(host) && re.RequestURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        int pos = re.RequestURL.IndexOf("/", 8);
                        if (!host.EndsWith("/"))
                        {
                            host += "/";
                        }
                        this.txtRequestUrl.Text = (host + re.RequestURL.Substring(pos + 1)).Replace("\r", "");
                    }
                    else
                    {
                        this.txtRequestUrl.Text = re.RequestURL.Replace("\r", "");
                    }

                    this.txtRequestHeader.Text = re.RequestHeader;
                    this.txtRequestBody.Text = re.RequestBody;
                    this.txtSaveAs.Text = selected;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            try
            {
                SharedHttpHeaderSettingEntity settingObj = new SharedHttpHeaderSettingEntity();
                settingObj.ContentType = this.txtContentType.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.AppId = this.txtAppID.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.AppSecret = txtAppSecret.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.GetTokenRequestUrl = this.txtRequestUrlForGettingToken.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.GetTokenRequestBody = txtRequestBodyForGettingToken.Text.Trim().Replace("\r\n", String.Empty);

                settingObj.Agency = txtAgency.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.Environment = txtEnvironment.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.AccessKey = txtAccessKey.Text.Trim().Replace("\r\n", String.Empty);

                if (string.IsNullOrWhiteSpace(settingObj.ContentType)
                    || string.IsNullOrWhiteSpace(settingObj.AppId)
                    || string.IsNullOrWhiteSpace(settingObj.AppSecret)
                    || string.IsNullOrWhiteSpace(settingObj.GetTokenRequestUrl)
                    || string.IsNullOrWhiteSpace(settingObj.GetTokenRequestBody)
                    || string.IsNullOrWhiteSpace(settingObj.Agency)
                    || string.IsNullOrWhiteSpace(settingObj.Environment)
                    || this.cmbHostsTab2.SelectedItem == null
                    )
                {
                    MessageBox.Show("Please enter all required fields.");
                    return;
                }
                var selectedHost = this.cmbHostsTab2.SelectedItem as HostEntity;
                FileUtility.WriteHttpHeaderSettingFile(settingObj, selectedHost);


                MessageBox.Show("Update Successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void btnGenerateToken_Click(object sender, EventArgs e)
        {
            try
            {
                SharedHttpHeaderSettingEntity settingObj = new SharedHttpHeaderSettingEntity();
                settingObj.ContentType = this.txtContentType.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.AppId = this.txtAppID.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.AppSecret = txtAppSecret.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.GetTokenRequestUrl = this.txtRequestUrlForGettingToken.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.GetTokenRequestBody = txtRequestBodyForGettingToken.Text.Trim().Replace("\r\n", String.Empty);

                settingObj.Agency = txtAgency.Text.Trim().Replace("\r\n", String.Empty);
                settingObj.Environment = txtEnvironment.Text.Trim().Replace("\r\n",String.Empty);
                settingObj.AccessKey = txtAccessKey.Text.Trim().Replace("\r\n", String.Empty);

                if(string.IsNullOrWhiteSpace(settingObj.ContentType)
                    || string.IsNullOrWhiteSpace(settingObj.AppId)
                    || string.IsNullOrWhiteSpace(settingObj.AppSecret)
                    || string.IsNullOrWhiteSpace(settingObj.GetTokenRequestUrl)
                    || string.IsNullOrWhiteSpace(settingObj.GetTokenRequestBody)
                    || string.IsNullOrWhiteSpace(settingObj.Agency)
                    || string.IsNullOrWhiteSpace(settingObj.Environment)
                    || this.cmbHostsTab2.SelectedItem == null
                    )
                {
                    MessageBox.Show("Please enter all required fields.");
                    return;
                }
                var selectedHost = this.cmbHostsTab2.SelectedItem as HostEntity;

                FileUtility.WriteHttpHeaderSettingFile(settingObj, selectedHost);
                SHARED_HTTP_HEADER_SETTING = settingObj;

                // send request to get access token
                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
                HttpClient client = new HttpClient();
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, settingObj.GetTokenRequestUrl);
                requestMessage.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

                requestMessage.Content = new StringContent(settingObj.GetTokenRequestBody, Encoding.UTF8, "application/x-www-form-urlencoded");
                
                var task = client.SendAsync(requestMessage,HttpCompletionOption.ResponseContentRead);
                
                await task.ContinueWith((requestTask) =>
                {
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                         settingObj.AccessToken = this.txtResponseToken.Text =ParseToken( requestTask.Result.Content.ReadAsStringAsync().Result);
                         FileUtility.WriteHttpHeaderSettingFile(settingObj, selectedHost);
                    }
                    else
                    {
                        MessageBox.Show("Failed to generate access token, please try again.");
                        return;
                    }
                });
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void GenerateToken(string requestURL, string requestBody)
        {
            if (String.IsNullOrWhiteSpace(requestURL) || String.IsNullOrWhiteSpace(requestBody))
            {
                 throw new ArgumentNullException("requestURL or requestBody");
            }
            // send request to get access token
            ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
            HttpClient client = new HttpClient();
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, requestURL);
            requestMessage.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

            requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");

            var task = client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead);

            task.ContinueWith((response) =>
            {
                if (response.IsCompleted && response.Status == TaskStatus.RanToCompletion)
                {
                    if (SHARED_HTTP_HEADER_SETTING == null)
                        SHARED_HTTP_HEADER_SETTING = new SharedHttpHeaderSettingEntity();

                    SHARED_HTTP_HEADER_SETTING.AccessToken = ParseToken(response.Result.Content.ReadAsStringAsync().Result);
                }
                else
                {
                    MessageBox.Show("Failed to generate access token, please try again.");
                    return;
                }
            });
        }

        private string ParseToken(string tokenObj)
        {
            //{"access_token":"BaTnHQjVRoUvUiKrpAav7ocnKK4NODi-e9eWjKJUvIK-Y6y4BuoqxQXLJvVzgeCedLhVfGI9Pb0hFng6qRxJFV4DWxq1F0TYdINZWRjVoufe_Qc3c5e9N_GvgoxNiv89bS76_9hq09yOyYwtkr1hsnRlanbocnSj4AH_t2l0qqjy8BZTIubP5AxStxQokOYx0QP1NWB5LQ7AEhQbdYCrlKMpce4dDXS4JPeTUqFd0crkh53Jt3ZQunGXCLhYgA2hnozbsxS8L40JOE3kqkLyu08jYawxdfcy7PNXwrpzZ5AoCkRKs2KrEfxdb0bVi4AEOE33xeJajfzMQxou6YtlGzcy-cBknZ-yELYuwvRAJoXLeAkfaHDhDILxMK0nNcZdO7Y8XJH4V19-J8dS-luc1IIcsaxBTvEszGRKo-h7l8P4vm4vFsjsvdlvZi-QpZecdv-YMfe0bmFp-Yiq47XbW_TPz92e-eEM6qQ4l3A6RYXJ4T6_wQVELnzmFXd6V-C3jvi9buLki2Ej9L2Tf0o30gRIT2NHgKiXgq41cZXKOq2vncuH9w5T4lIaJx_z8VL6kWUO3tFmwr2RF7QsxJA6MLm7Ciq3FfZTFT_e66obo7x8loJsMQeoCguEp4mMTOnYSJ0u15WCq-dhzM0dgwfEqAg-At09BHFeBBPND3YreEjszlezcJ6HiszVJNWRJx1k0",
            //"token_type":"bearer",
            //"expires_in":"86400",
            //"refresh_token":"NNn5!IAAAAHHjD5WnVDx_peUahDxY1S2GwUfAs6UmOYyvJrc9XKT7wQEAAAHmnNAdQMzjUmTi2a7xh3ic4Z-ms_syKyCyfW4WDT7M01azBU3sMc1UVSrzx9qKjloXg6bfo33uijWxw_sdThEDuA7n5_ljpX_JTH152tu6vZsz1UjYD_EYg2QN1j4d4hoM7pFfanGq8moou7IOt7xEoloZyTw7pK5qVbyDkYRXTn-ZvGGI8GeG0Y63YI_MEOA_n8hwZJzqpgWVGNu8pTXJTpE1DKUXJZi7JJjTzh7KaiBxLFIcDPcyFLKJ24MyL7BONxypPZWP1OmjVzl9HqrXPmWLObx8PNbli8OkVX4T2hGW9vADlBowiaYDxYnLEactaib5dXE92v8ArjpJMUiHKPothmQWgTVTdgNeG-YKcXx1uOOUKwMBAbQixuuIqvQDzv9K_rKj-X8g4NdXaCfbfxNbeEtG195MGDjYqNyT_6O09g1UtXiAYsLZ0oa5tedIJHJxqf_sZ2Zrmv2OJZAvMg9bVBSlJ-ysZ9TWq3JDoG2RHvd8QX0v4HRU9PUjxOD_gmnNdSt8MhvSilEffLmFsvJ510Lk6t8QIzOzllv3tTMep6Q7jKbkasVL1uMzGRL50mor9WB_Df0P5KYixTg1",
            //"scope":"addresses conditions contacts documents get_civicid_profile get_inspection_checklist_items get_record_customtables get_settings_record_statuses inspections owners parcels payments professionals records reports run_generic_search settings"}
            string error = "Invalid Token string, maybe error is raised while getting token.";
            if(String.IsNullOrWhiteSpace(tokenObj) || tokenObj.Length < 20)
            {
                throw new Exception(error);
            }

            int indexEndPos = tokenObj.IndexOf("\",\"");
            int indexStartPos = tokenObj.IndexOf("\":\"");

            if(indexEndPos == -1 || indexStartPos == -1 || indexEndPos <= indexStartPos)
            {
                throw new Exception(error);
            }

            if (tokenObj.StartsWith("{\"error\""))
            {
                return tokenObj;
            }
            string token = tokenObj.Substring(indexStartPos + 3, indexEndPos - indexStartPos - 3);

            return token;
        }

        private void txtRequestBodyForGettingToken_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control & e.KeyCode == Keys.A)
                txtRequestBodyForGettingToken.SelectAll();
        }

        private void txtResponseToken_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control & e.KeyCode == Keys.A)
                txtResponseToken.SelectAll();
        }

        private void txtEnvironment_TextChanged(object sender, EventArgs e)
        {
            ChangeRequestBodyByKey("environment", txtEnvironment.Text.Trim());
        }

        private void txtAgency_TextChanged(object sender, EventArgs e)
        {
            ChangeRequestBodyByKey("agency_name", txtAgency.Text.Trim());
        }

        private void txtAppSecret_TextChanged(object sender, EventArgs e)
        {
            ChangeRequestBodyByKey("client_secret", txtAppSecret.Text.Trim());
        }

        private void txtAppID_TextChanged(object sender, EventArgs e)
        {
            ChangeRequestBodyByKey("client_id", txtAppID.Text.Trim());
        }

        private void ChangeRequestBodyByKey(string key,string value)
        {
            string requestBody = this.txtRequestBodyForGettingToken.Text.Trim();
            var kvs = requestBody.Split('&');

            foreach(var s in kvs)
            {
                if(s.IndexOf(key) > -1)
                {
                    this.txtRequestBodyForGettingToken.Text = requestBody.Replace(s, String.Format("{0}={1}", key, value));
                    break;
                }
            }
        }

        private void RegenerateTokenWhenMoreThan24Hours()
        {
            if ((DateTime.Now - this.LastExecuteTime).Hours > 23)
            {
                this.LastExecuteTime = DateTime.Now;
                btnGenerateToken_Click(null, null);
            }
        }

        private void btnHostSave_Click(object sender, EventArgs e)
        {
            try
            {
                var hosts = new List<HostEntity>();

                string content = this.txtAllHosts.Text.Trim();

                if (!String.IsNullOrWhiteSpace(content))
                {
                    var lines = content.Split(new char[] { '\r', '\n' });
                    lines.ToList().Where(l => !String.IsNullOrWhiteSpace(l)).ToList().
                        ForEach(l =>
                        {
                            var keyValue = l.Split(new char[] { ' ' });
                            if(keyValue.Length == 1)
                            {
                                hosts.Add(new HostEntity { Name = keyValue[0], HostUrl = String.Empty });
                            }
                            else if (keyValue.Length == 2)
                            {
                                hosts.Add(new HostEntity { Name = keyValue[0], HostUrl = keyValue[1] });
                            }
                        }
                );
                }


                if (hosts == null || hosts.Count == 0)
                {
                    MessageBox.Show("Please check the format, At least one host is required.");
                    return;
                }
                else
                {
                    FileUtility.WriteHostsToFile(hosts);
                    HOSTS = hosts;
                    MessageBox.Show("Save successfully.");
                    //BindHostsToDataGrid(HOSTS);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //private void BindHostsToDataGrid(List<HostEntity> hosts)
        //{
        //    DataGridViewTextBoxColumn column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
        //    column1.DataPropertyName = "Name";
        //    column1.HeaderText = "Name";
        //    column1.Name = "Name";
        //    column1.Width = 200;

        //    DataGridViewTextBoxColumn column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
        //    column2.DataPropertyName = "HostUrl";
        //    column2.HeaderText = "HostUrl";
        //    column2.Name = "HostUrl";
        //    column2.Width = 500;

        //    dataGridView1.AllowUserToAddRows = true;
        //    this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {column1, column2 });

        //    this.dataGridView1.DataSource = hosts;
        //}

        private void ShowHostsToHostsText(List<HostEntity> hosts)
        {
            StringBuilder sbContent = new StringBuilder();
            if(hosts != null)
            {
                int i = 0;
                foreach(var h in hosts)
                {
                    if(i>0)
                    {
                        sbContent.Append("\r\n");
                    }
                    
                    sbContent.Append(String.Format("{0} {1}", h.Name, h.HostUrl));
                    i++;
                }
            }

            this.txtAllHosts.Text = sbContent.ToString();
        }

        private void txtAllHosts_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control & e.KeyCode == Keys.A)
                txtAllHosts.SelectAll();
        }

        private void cmbHosts_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                // get host
                string host = "";
                string requestUrl = "";
                var selectHost = this.cmbHosts.SelectedItem as HostEntity;
                if (selectHost == null)
                {
                    return;
                }

                host = selectHost.HostUrl;
                requestUrl = this.txtRequestUrl.Text.Trim();

                if (!String.IsNullOrWhiteSpace(host) && requestUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    int pos = requestUrl.IndexOf("/", 8);
                    if (!host.EndsWith("/"))
                    {
                        host += "/";
                    }
                    this.txtRequestUrl.Text = (host + requestUrl.Substring(pos + 1)).Replace("\r", "");
                }

                LoadSharedHttpHeaderSetting(selectHost);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void cmbHostsTab2_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectHost = this.cmbHostsTab2.SelectedItem as HostEntity;
            //if (selectHost == null)
            //{
            //    return;
            //}

            LoadSharedHttpHeaderSetting(selectHost);
        }

        private async void btnBBB_AddBrick_Click(object sender, EventArgs e)
        {
            Log.WriteLog("in addBrick method");
            this.txtResponse2.Clear();
           
            //// 1. get user token
            //var userName = this.txtBBB_loginName.Text.Trim();
            //var password = this.txtBBB_Password.Text.Trim();

            // 1. get all user list - user name and password
            bool first = true;
            foreach (var account in BBMAccounts.Accounts)
            {
                if(first)
                {
                    first = false;
                }
                else
                {
                    System.Threading.Thread.Sleep(1000 * 3);
                }
               
                var userName = account.UserName;
                var password = account.Password;

                if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                {
                    //MessageBox.Show("login name and password are required.");
                    Log.WriteLog("Error: login name and password are required.");
                    continue;
                }

                string token = await this.GetBBBHomeToken2(userName, password);

                // 2. Add brick
                //var url = "https://wallet.bbb-home.com/app/game/addReadInformationBrick";

                var articleidStart = Convert.ToInt32(this.txtBBB_idstart.Text.Trim());
                var articleidEnd = Convert.ToInt32(this.txtBBB_IdEnd.Text.Trim());


                if (string.IsNullOrWhiteSpace(token))
                {
                    Log.WriteLog("Error: Token is null. userName:" + userName + ", password:" + password);
                    continue;
                }
                //            string requestHeader0 = @"Accept: application/json, text/plain, */*
                //Authorization: {0}
                //Content-Type: application/x-www-form-urlencoded
                //Accept-Encoding: gzip, deflate, br
                //Accept-Language: en-US,en;q=0.8";
                //var header = String.Format(requestHeader0, token);

                for (int i = articleidStart; i <= articleidEnd; i++)
                {

                    if (!((i>=85 && i<90) || i==110 || i==112 || i >=114))
                        continue;
                    Log.WriteLog("========================= " + i + " ==========================");
                    txtResponse2.Text = "\r\n " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + "========================= " + i + " ==========================" + this.txtResponse2.Text;
                    await addBrick(token, i);
                }

                // 3. check in/go sign
                string requestUrl_goSignin = "https://wallet.bbb-home.com/app/game/goSignIn";
                await this.SendRequestToBBM(token, requestUrl_goSignin, "");


                // 4. build hourse
                string requstUrl_buildhouse = "https://wallet.bbb-home.com/app/game/buildHouse";

                bool buildHouse = true;
                while (buildHouse)
                {
                    try
                    {
                        string response_buildhouse = await this.SendRequestToBBM(token, requstUrl_buildhouse, "");
                        JObject buildHouseObj = JObject.Parse(response_buildhouse);
                        string buldHouseMsg = Convert.ToString(buildHouseObj["msg"]);
                        if (!buldHouseMsg.Equals("成功", StringComparison.InvariantCultureIgnoreCase))
                        {
                            buildHouse = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        buildHouse = false;
                        Log.WriteLog("exception: " + ex.ToString());
                    }
                }

                // 5. add bbm 
                string requstUrl_addBBM = "https://wallet.bbb-home.com/app/game/addUserBBM";
                // 5.1 get the bbmNum that server side will check the num
                string requstUrl_getUserIncomeBBMNum = "https://wallet.bbb-home.com/app/game/getUserIncomeBBMNum";

                string response_getUserIncomeBBMNum = await this.SendRequestToBBM(token, requstUrl_getUserIncomeBBMNum, "");
                JObject bbmNumObj = JObject.Parse(response_getUserIncomeBBMNum);
                string bbmNum = Convert.ToString(bbmNumObj["bubbleBBMNum"]);
                int bbmCallTimes = Convert.ToInt32(bbmNumObj["bubbleNum"]);

                string requestBody_addBBM = "bbmNum=" + bbmNum;

                for (int i = 0; i <= bbmCallTimes; i++)
                {
                    Log.WriteLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + " call add bmm: " + i + " bbmNum" + bbmNum);
                    await this.SendRequestToBBM(token, requstUrl_addBBM, requestBody_addBBM);
                }
            }

            txtResponse2.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") +  " Completed \r\n" + txtResponse2.Text;
        }

        private async Task<string> GetBBBHomeToken(string userName, string password)
        {
            string token = "";
            try
            {
                string msg = "userName:" + userName + " password:" + password;
                Log.WriteLog(msg);
                this.txtResponse2.Text += "\r\n" + msg;
                //            POST https://wallet.bbb-home.com/login HTTP/1.1
                //Host: wallet.bbb-home.com
                //Connection: keep-alive
                //Content-Length: 48
                //Accept: application/json, text/javascript, */*; q=0.01
                //Origin: http://game.bjex.vip
                //User-Agent: Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36
                //Content-Type: application/json;charset=UTF-8
                //Referer: http://game.bjex.vip/login.html
                //Accept-Encoding: gzip, deflate, br
                //Accept-Language: en-US,en;q=0.8

                //{"username":"18566255573","password":"19501117"}

                string requestUrl = "https://wallet.bbb-home.com/login";
                //string method = "POST";
                string requestHeader0 = @"Accept: application/json, text/plain, */*
Content-Type: application/x-www-form-urlencoded
Accept-Encoding: gzip, deflate, br
Accept-Language: en-US,en;q=0.8";

                //string requestBody1 = @"{""username"":""{0}"",""password"":""{1}""}";
                //string requestBody = string.Format(requestBody1, userName, password);
                string requestBody2 = "{\"username\":\"" + userName + "\",\"password\":\"" + password + "\"}";
                //parse respose body to get token
                /*
 
                 * HTTP/1.1 200
    Server: nginx
    Date: Sun, 06 May 2018 07:59:55 GMT
    Content-Type: text/html;charset=UTF-8
    Content-Length: 321
    Connection: keep-alive
    X-Content-Type-Options: nosniff
    X-XSS-Protection: 1; mode=block
    Cache-Control: no-cache, no-store, max-age=0, must-revalidate
    Pragma: no-cache
    Expires: 0
    X-Frame-Options: DENY
    Access-Control-Allow-Origin: http://game.bjex.vip
    Vary: Origin
    Access-Control-Allow-Credentials: true
    Authorization: GoldBee eyJhbGciOiJIUzUxMiJ9.eyJzdWIiOiIxNTQ4ODAiLCJleHAiOjE1MjYwMjU1OTV9.7E9tPuAhQmTwP2r9KbUozVD26XLRypJ17pq4FlBVEeJVj2hruVv5yLfrUviK2h6E3kuSb-qFvIeJagtBcJExbg

    {"msg":"成功","data":{"address":"jsLoxmn4Cj2vFBupWbEaBzSWTJyrdiLzWh","uname":"李玉珍","isAuthority":1,"tmd":"GoldBee eyJhbGciOiJIUzUxMiJ9.eyJzdWIiOiIxNTQ4ODAiLCJleHAiOjE1MjYwMjU1OTV9.7E9tPuAhQmTwP2r9KbUozVD26XLRypJ17pq4FlBVEeJVj2hruVv5yLfrUviK2h6E3kuSb-qFvIeJagtBcJExbg","account":"李玉珍","ispwd":2},"retcode":0}
                 * 
                 */



                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
                HttpClient client = new HttpClient();
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);


                requestMessage.Content = new StringContent(requestBody2, Encoding.UTF8, "application/json");


                var task = client.SendAsync(requestMessage);
                string responseString = null;
                await task.ContinueWith((requestTask) =>
                {
                    // Get HTTP response from completed task.
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                        //    HttpResponseMessage response = requestTask.Result;
                        var header = requestTask.Result.Content.Headers.ToString();
                        this.txtResponseHeader.Text = ((int)requestTask.Result.StatusCode) + "-" + requestTask.Result.StatusCode.ToString() + "\r\n" + header + requestTask.Result.Headers.ToString();
                        //response.Content.Headers.ContentType
                        var resultStream = requestTask.Result.Content.ReadAsStreamAsync().Result;
                        if (requestTask.Result.Content.Headers.ContentEncoding.Count > 0)
                        {
                            var contentEncoding = requestTask.Result.Content.Headers.ContentEncoding.First();
                            resultStream = Decompress(resultStream, contentEncoding);
                            resultStream.Position = 0;
                            StreamReader reader = new StreamReader(resultStream);
                            responseString = reader.ReadToEnd();
                            resultStream.Close();
                            reader.Close();
                        }
                        else
                        {
                            responseString = requestTask.Result.Content.ReadAsStringAsync().Result;
                        }

                        //this.txtResponse.Text=responseString;

                    }
                    else
                    {
                        var error = "";
                    }

                });

                token = await ParseBBBToken(responseString);
            }
            catch(Exception ex)
            {
                Log.WriteLog(ex.ToString());
                this.txtResponse2.Text += ex.ToString();
            }

            Log.WriteLog("token: " + token);
            this.txtResponse2.Text += "\r\n " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + "token: " + token;
            return token;
        }

        private async Task<string> GetBBBHomeToken2(string userName, string password)
        {
            string token = "";
            try
            {
                string msg = "userName:" + userName + " password:" + password;
                Log.WriteLog(msg);
                this.txtResponse2.Text += "\r\n" + msg;
                //            POST https://wallet.bbb-home.com/login HTTP/1.1
                //Host: wallet.bbb-home.com
                //Connection: keep-alive
                //Content-Length: 48
                //Accept: application/json, text/javascript, */*; q=0.01
                //Origin: http://game.bjex.vip
                //User-Agent: Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36
                //Content-Type: application/json;charset=UTF-8
                //Referer: http://game.bjex.vip/login.html
                //Accept-Encoding: gzip, deflate, br
                //Accept-Language: en-US,en;q=0.8

                //{"username":"18566255573","password":"19501117"}

                var client = new RestClient("https://wallet.bbb-home.com/login");
                var request = new RestRequest(Method.POST);
     
                request.AddHeader("Cache-Control", "no-cache");
                request.AddParameter("undefined", "{\"username\":\"" + userName + "\",\"password\":\""+ password+"\"}", ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                foreach(var h in response.Headers)
                {
                    if(h.Name =="Authorization")
                    {
                        token = h.Value.ToString();
                        break;
                    }
                }

               // token = await ParseBBBToken(responseString);
            }
            catch (Exception ex)
            {
                Log.WriteLog(ex.ToString());
                this.txtResponse2.Text += ex.ToString();
            }

            Log.WriteLog("token: " + token);
            this.txtResponse2.Text += "\r\n " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + "token: " + token;
            return token;
        }

        private async void btnGetBBBToken_Click(object sender, EventArgs e)
        {
              string token = await GetBBBHomeToken("18566255573", "19501117");

              this.txtResponse.Text = token;
        }

        private async Task<string> ParseBBBToken(string responseString)
        {
            JObject tokenObj = JObject.Parse(responseString);
            string token = Convert.ToString(tokenObj["data"]["tmd"]);

            return token;
        }

        private async Task addBrick(string token, int id)
        {
            string responseString = null;
            try
            {


                string requestUrl = "https://wallet.bbb-home.com/app/game/addReadInformationBrick";

                //string requestBody1 = @"{""username"":""{0}"",""password"":""{1}""}";
                //string requestBody = string.Format(requestBody1, userName, password);
                string requestBody = "id=" + id;
      
                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
                HttpClient client = new HttpClient();
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                requestMessage.Headers.Add("Authorization", token);
                //requestMessage.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");


                var task = client.SendAsync(requestMessage);

                await task.ContinueWith((requestTask) =>
                {
                    // Get HTTP response from completed task.
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                        //    HttpResponseMessage response = requestTask.Result;
                        var header = requestTask.Result.Content.Headers.ToString();
                        this.txtResponseHeader.Text = ((int)requestTask.Result.StatusCode) + "-" + requestTask.Result.StatusCode.ToString() + "\r\n" + header + requestTask.Result.Headers.ToString();
                        //response.Content.Headers.ContentType
                        var resultStream = requestTask.Result.Content.ReadAsStreamAsync().Result;
                        if (requestTask.Result.Content.Headers.ContentEncoding.Count > 0)
                        {
                            var contentEncoding = requestTask.Result.Content.Headers.ContentEncoding.First();
                            resultStream = Decompress(resultStream, contentEncoding);
                            resultStream.Position = 0;
                            StreamReader reader = new StreamReader(resultStream);
                            responseString = reader.ReadToEnd();
                            resultStream.Close();
                            reader.Close();
                        }
                        else
                        {
                            responseString = requestTask.Result.Content.ReadAsStringAsync().Result;
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                Log.WriteLog(ex.ToString());
                this.txtResponse2.Text = "\r\n" + ex.ToString() + this.txtResponse2.Text;
            }

            Log.WriteLog("\r\naddBrick: " + responseString);
            this.txtResponse2.Text = "\r\naddBrick: " + responseString + this.txtResponse2.Text;
            return;
        }



        private async Task<string> SendRequestToBBM(string token,string requestUrl, string requestBody)
        {
            string responseString = null;
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
                HttpClient client = new HttpClient();
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                if(!string.IsNullOrWhiteSpace(token))
                    requestMessage.Headers.Add("Authorization", token);
                requestMessage.Headers.Add("Accept-Encoding", "br, gzip, deflate");

                requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");


                var task = client.SendAsync(requestMessage);

                await task.ContinueWith((requestTask) =>
                {
                    // Get HTTP response from completed task.
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                        //    HttpResponseMessage response = requestTask.Result;
                        var header = requestTask.Result.Content.Headers.ToString();
                        this.txtResponseHeader.Text = ((int)requestTask.Result.StatusCode) + "-" + requestTask.Result.StatusCode.ToString() + "\r\n" + header + requestTask.Result.Headers.ToString();
                        //response.Content.Headers.ContentType
                        var resultStream = requestTask.Result.Content.ReadAsStreamAsync().Result;
                        if (requestTask.Result.Content.Headers.ContentEncoding.Count > 0)
                        {
                            var contentEncoding = requestTask.Result.Content.Headers.ContentEncoding.First();
                            resultStream = Decompress(resultStream, contentEncoding);
                            resultStream.Position = 0;
                            StreamReader reader = new StreamReader(resultStream);
                            responseString = reader.ReadToEnd();
                            resultStream.Close();
                            reader.Close();
                        }
                        else
                        {
                            responseString = requestTask.Result.Content.ReadAsStringAsync().Result;
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                Log.WriteLog(ex.ToString());
                this.txtResponse2.Text = "\r\n" + ex.ToString() + this.txtResponse2.Text;
            }

            Log.WriteLog("\r\n " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + " request: " + requestUrl + " response: " + responseString);
            this.txtResponse2.Text = "\r\n " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + " request: " + requestUrl + " response: " + responseString + this.txtResponse2.Text;
            return responseString;
        }

        private async void btnShowAllBBMUsers_Click(object sender, EventArgs e)
        {
            try
            {
                this.dataGridView1.Rows.Clear();
                List<BBMUserInfo> users = new List<BBMUserInfo>();

                foreach (var account in BBMAccounts.Accounts)
                {
                    var userName = account.UserName;
                    var password = account.Password;

                    if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                    {
                        //MessageBox.Show("login name and password are required.");
                        Log.WriteLog("Error: login name and password are required.");
                        continue;
                    }

                    string token = await this.GetBBBHomeToken(userName, password);

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        Log.WriteLog("Error: Token is null. userName:" + userName + ", password:" + password);
                        continue;
                    }

                    string requstUrl_getUserInfo = "https://wallet.bbb-home.com/app/game/getUserInfo";


                    try
                    {
                        string response_UserInfo = await this.SendRequestToBBM(token, requstUrl_getUserInfo, "");
                        /*
                            {"lesseeNum":1,"avatarAddress":"https://console.bbb-home.com/console/fileStore/1/2018/4/17/357.png",
                        "vacancyNum":9,"appDownloadAddress":"http://fir.im/5mue",
                            * "inviteLink":"http://game.bjex.vip/invite.html?inviteCode=ydagpd",
                            * "bbmQuantity":48.000000,
                            * "nickname":"10",
                            * "bbmForce":20,
                            * "floorNum":1,
                            * "isSignIn":1}
                            * */
                        JObject userObj = JObject.Parse(response_UserInfo);

                        var user = new BBMUserInfo
                        {
                            UserName = userName,
                            NickName = Convert.ToString(userObj["nickname"]),
                            BBMProductivity = Convert.ToString(userObj["bbmForce"]),
                            BBMQuantity = Convert.ToString(userObj["bbmQuantity"]),
                            TotalFloors = Convert.ToString(userObj["floorNum"]),
                            CheckedIn = Convert.ToString(userObj["isSignIn"]),
                            VacancyNum = Convert.ToString(userObj["vacancyNum"])
                        };
                        users.Add(user);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLog("exception: " + ex.ToString());
                    }
                }

                // add summary line
                var totalItem = new BBMUserInfo();

                foreach(var item in users)
                {
                    totalItem.BBMQuantity = (Convert.ToInt32(item.BBMQuantity) + Convert.ToInt32(totalItem.BBMQuantity)).ToString();
                }
                users.Add(totalItem);
                this.dataGridView1.DataSource = users;

            }
            catch(Exception ex1)
            {
                Log.WriteLog("exception: " + ex1.ToString());
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
               
            string requstUrl = "https://wallet.bbb-home.com/app/game/initUserInfo";

            string userNames = this.txtMobiles.Text.Trim();
            var names = userNames.Split(new char[] { '\r', '\n' },StringSplitOptions.RemoveEmptyEntries);

            foreach (var name in names)
            {
                var sname = name.Trim();
                string token = await this.GetBBBHomeToken(sname, "111111");

                try
                {
                    string response = await this.SendRequestToBBM(token, requstUrl, "avatarId=2&nickname=" + sname);
                    this.txtResponse_intiuser.Text +="\r\n" + response;
                }
                catch (Exception ex)
                {
                    Log.WriteLog("exception in initUserInfo: " + ex.ToString());
                }
            }

            this.txtResponse_intiuser.Text +="\r\n" + "Init users done.";

        }

        private async void btnRegister_Click(object sender, EventArgs e)
        {
            //string url = "http://51ym.me/User/MobileSMSCode.aspx";
            //var response = await SendRequestM(HttpMethod.Get, url, null);
            var recommentCode = this.txtRecommendCode.Text.Trim();
            if(string.IsNullOrWhiteSpace(recommentCode))
            {
                MessageBox.Show("recommend code is required.");
                return;
            }

            int num = Convert.ToInt32(this.txtUserNumber.Text.Trim());

            for (int i = 0; i < num; i++)
            {

                // 1.get phone number: http://api.fxhyd.cn/appapi.aspx?callback=jQuery22305168274965513823_1526054060076&jsonp=MobileSeachJsonCallback&actionid=getmobile&token=0053105437bcf9885691675be62916bed6f3adfd&itemid=15224&province=0&city=0&isp=0&mobile=&excludeno=&_=1526054060077
                // response:
                //MobileSeachJsonCallback({"actionid":"getmobile","tradeid":"","error":{"errcode":0,"errmsg":"","showflag":"0"},"sign":"",
                //"data":{"draw":0,"total":0,"model":"18473879572","list":[]}})
                var url = "http://api.fxhyd.cn/appapi.aspx?callback=jQuery22305168274965513823_1526054060076&jsonp=MobileSeachJsonCallback&actionid=getmobile&token=0053105437bcf9885691675be62916bed6f3adfd&itemid=15224&province=0&city=0&isp=0&mobile=&excludeno=&_=1526054060077";
                var response = await SendRequestM(HttpMethod.Get, url, null);

                int index = response.IndexOf("\"model\":\"") + "\"model\":\"".Length;
                string phone = response.Substring(index, 11);

                // 2. get sim verify code
                // 2.1 send to request the code
                url = "https://wallet.bbb-home.com/app/user/getCode?type=1&phone=" + phone;
                response = await SendRequestM(HttpMethod.Get, url, null);
                await SendRequestM(HttpMethod.Get, url, null);

                // 2.2 get verify code
                // http://api.fxhyd.cn/appapi.aspx?callback=jQuery22308473370800415757_1526126007214&jsonp=GetSMSJsonCallback&actionid=getsms&token=0053105437bcf9885691675be62916bed6f3adfd&itemid=15224&mobile=13157011524&release=1&_=1526126007888
                // response:
                // GetSMSJsonCallback({"actionid":"getsms","tradeid":"","error":{"errcode":0,"errmsg":"","showflag":"0"},"sign":"",
                //"data":{"draw":0,"total":0,"model":"【币家钱包】验证码：1522，币家用户，您正进行注册操作。短信验证码请注意保密，如非本人操作请尽快联系客服！","list":[]}})

                url = String.Format("http://api.fxhyd.cn/appapi.aspx?callback=jQuery22308473370800415757_1526126007214&jsonp=GetSMSJsonCallback&actionid=getsms&token=0053105437bcf9885691675be62916bed6f3adfd&itemid=15224&mobile={0}&release=1&_=1526126007888", phone);

                string verifiedCode = "";
                bool getVerifiedCodeFailed = false;
                int callGetCode = 0;
                while (String.IsNullOrWhiteSpace(verifiedCode))
                {
                    response = await SendRequestM(HttpMethod.Get, url, null);
                    //【币家钱包】验证码：
                    index = response.IndexOf("【币家钱包】验证码：");
                    if (index < 0)
                    {
                        System.Threading.Thread.Sleep(2000);
                        callGetCode++;

                        if(callGetCode > 15)
                        {
                            getVerifiedCodeFailed = true;
                            break;
                        }
                    }
                    else
                    {
                        getVerifiedCodeFailed = false;
                        verifiedCode = response.Substring(index + "【币家钱包】验证码：".Length, 4);
                        Log.WriteLog("Phone:" + phone + " code: " + verifiedCode);
                    }

                }

                if (getVerifiedCodeFailed)
                {
                    break;
                }

                // release phone number
                //http://api.fxhyd.cn/appapi.aspx?callback=jQuery22308435976015303064_1526053622018&jsonp=MobileReleaseJsonCallback&actionid=release&token=0053105437bcf9885691675be62916bed6f3adfd&itemid=15224&mobile=15584159750&_=1526053622049
                //url = String.Format("http://api.fxhyd.cn/appapi.aspx?callback=jQuery22308435976015303064_1526053622018&jsonp=MobileReleaseJsonCallback&actionid=release&token=0053105437bcf9885691675be62916bed6f3adfd&itemid=15224&mobile={0}&_=1526053622049", phone);
                //response = await SendRequestM(HttpMethod.Get, url, null);
                // 3. create bbm account(mobile phone, password, c with ref user
                // https://wallet.bbb-home.com/app/user/register
                // phone=13052119237&code=24674&passWord=111111&userCode=yck3t7
                url = "https://wallet.bbb-home.com/app/user/register";
                var requestBody = String.Format("phone={0}&code={1}&passWord={2}&userCode={3}", phone, verifiedCode, "111111", recommentCode);
                response = await SendRequestM(HttpMethod.Post, url, requestBody);

                if (response.IndexOf("403 Forbidden") > -1)
                {
                    Log.WriteLog("BBM Forbidden");
                    this.txtRegisterResponse.Text = "\r\n403 Forbidden" + this.txtRegisterResponse.Text;
                    return;
                }
                // 4. init necessary
                url = "https://wallet.bbb-home.com/app/game/initUserInfo";

                string token = await this.GetBBBHomeToken(phone, "111111");

                try
                {
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        Log.WriteLog("BBM token is null");
                    }
                    else
                    {
                        response = await this.SendRequestToBBM(token, url, "avatarId=2&nickname=" + phone);
                        Log.WriteLog("User Register:" + response);

                        this.txtRegisterResponse.Text = response + "\r\n" + this.txtRegisterResponse.Text;
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLog("exception in initUserInfo: " + ex.ToString());
                }
            }
        }

        private async Task<string> SendRequestM(HttpMethod method, string requestUrl, string requestBody, string authorization = null, string contenttype=null)
        {
            string responseString = null;

            Log.WriteLog("\r\n request url: " + requestUrl + " \r\n RequestBody:" + requestBody + "\r\n"); 
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
                HttpClient client = new HttpClient();
                HttpRequestMessage requestMessage = new HttpRequestMessage(method, requestUrl);
                if(authorization != null)
                    requestMessage.Headers.Add("Authorization", authorization);
                if (!String.IsNullOrWhiteSpace(contenttype))
                    requestMessage.Headers.Add("Content-Type", contenttype);

                if(method!=HttpMethod.Get)
                    requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");


                var task = client.SendAsync(requestMessage);

                await task.ContinueWith((requestTask) =>
                {
                    // Get HTTP response from completed task.
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                        //    HttpResponseMessage response = requestTask.Result;
                        var header = requestTask.Result.Content.Headers.ToString();
                        this.txtResponseHeader.Text = ((int)requestTask.Result.StatusCode) + "-" + requestTask.Result.StatusCode.ToString() + "\r\n" + header + requestTask.Result.Headers.ToString();
                        //response.Content.Headers.ContentType
                        var resultStream = requestTask.Result.Content.ReadAsStreamAsync().Result;
                        if (requestTask.Result.Content.Headers.ContentEncoding.Count > 0)
                        {
                            var contentEncoding = requestTask.Result.Content.Headers.ContentEncoding.First();
                            resultStream = Decompress(resultStream, contentEncoding);
                            resultStream.Position = 0;
                            StreamReader reader = new StreamReader(resultStream);
                            responseString = reader.ReadToEnd();
                            resultStream.Close();
                            reader.Close();
                        }
                        else
                        {
                            responseString = requestTask.Result.Content.ReadAsStringAsync().Result;
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                Log.WriteLog(ex.ToString());
            }

            Log.WriteLog("\r\nresponse: " + responseString + "\r\n=============================\r\n");
           
            return responseString;
        }

        private async Task<string> SendRequestMultiForm(string requestUrl, string requestBody, string boundary)
        {
            string responseString = null;

            Log.WriteLog("\r\n request url: " + requestUrl + " \r\n RequestBody:" + requestBody + "\r\n");
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
                HttpClient client = new HttpClient();
                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            //    using (var content =
            //new MultipartFormDataContent(boundary)
            //    {
            //        content.Add(new StreamContent(new MemoryStream(image)), "bilddatei", "upload.jpg");

            //        using (
            //           var message =
            //               await client.PostAsync("http://www.directupload.net/index.php?mode=upload", content))
            //        {
            //            var input = await message.Content.ReadAsStringAsync();

            //            return !string.IsNullOrWhiteSpace(input) ? Regex.Match(input, @"http://\w*\.directupload\.net/images/\d*/\w*\.[a-z]{3}").Value : null;
            //        }
            //    }

                //requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, "multipart/form-data; boundary=" + boundary);
                //requestMessage.Content = new MultipartFormDataContent(boundary);
                var multipartContent = new MultipartFormDataContent(boundary);
                multipartContent.Add(new StringContent(requestBody, Encoding.UTF8));
                requestMessage.Content = multipartContent;

                var task = client.SendAsync(requestMessage);

                await task.ContinueWith((requestTask) =>
                {
                    // Get HTTP response from completed task.
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                        //    HttpResponseMessage response = requestTask.Result;
                        var header = requestTask.Result.Content.Headers.ToString();
                        this.txtResponseHeader.Text = ((int)requestTask.Result.StatusCode) + "-" + requestTask.Result.StatusCode.ToString() + "\r\n" + header + requestTask.Result.Headers.ToString();
                        //response.Content.Headers.ContentType
                        var resultStream = requestTask.Result.Content.ReadAsStreamAsync().Result;
                        if (requestTask.Result.Content.Headers.ContentEncoding.Count > 0)
                        {
                            var contentEncoding = requestTask.Result.Content.Headers.ContentEncoding.First();
                            resultStream = Decompress(resultStream, contentEncoding);
                            resultStream.Position = 0;
                            StreamReader reader = new StreamReader(resultStream);
                            responseString = reader.ReadToEnd();
                            resultStream.Close();
                            reader.Close();
                        }
                        else
                        {
                            responseString = requestTask.Result.Content.ReadAsStringAsync().Result;
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                Log.WriteLog(ex.ToString());
            }

            Log.WriteLog("\r\nresponse: " + responseString + "\r\n=============================\r\n");

            return responseString;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Log.WriteLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + "timer starts \r\n");
            btnBBB_AddBrick_Click(null,null);
        }

        private async void btnTransfer_Click(object sender, EventArgs e)
        {
            try
            {
                string requestUrl = null;
                string requestBody = null;
                int index = 0;
                 foreach (var account in BBMAccounts.Accounts)
                 {
                     index++;

                     System.Threading.Thread.Sleep(2000);
                     // 1. get token
                     var token = await GetBBBHomeToken(account.UserName, account.Password);
                     if(string.IsNullOrWhiteSpace(token))
                     {
                         Log.WriteLog("\r\nError======= token is null for " + account.UserName + "======\r\n");
                         continue;
                     }
                     /*
                        2. get unreleased assets
                        post https://wallet.bbb-home.com/app/trading/getUnreleasedAssets
                        header:
                        Host: wallet.bbb-home.com
                        Accept: application/json
                        Authorization: GoldBee eyJhbGciOiJIUzUxMiJ9.eyJzdWIiOiIxNzgxIiwiZXhwIjoxNTI3OTQzNTA1fQ.HEILS5V_0QEetvHj-NNVjn2ak3ToCiJN_roZ0NwbPo5MOLo5FKqN3b62qUVVDaozKdGyukIpekq-DijyHvM1_Q
                        app_version: 1.1.3
                        X-Requested-With: XMLHttpRequest
                        Accept-Language: zh-cn
                        device_type: 1
                        Accept-Encoding: br, gzip, deflate
                        Content-Type: application/x-www-form-urlencoded


                        request body:
                        start=0&offset=10&isNext=1

                        Response:
                        {"msg":"成功","data":{"item":[{"wsf":1388470.485154,"bid":9}],"isNext":0},"retcode":0}

                      */
                     requestUrl ="https://wallet.bbb-home.com/app/trading/getUnreleasedAssets";
                     requestBody = "start=0&offset=10&isNext=1";
                     string response = await SendRequestToBBM(token, requestUrl, requestBody);

                     JObject obj = JObject.Parse(response);
                     string assetTotal = Convert.ToString(obj["data"]["item"][0]["wsf"]);

                     if (assetTotal == "0")
                         continue;
                     // 3. transfer to 138 account
                     /*
                        Accept: application/json
                        Authorization: GoldBee eyJhbGciOiJIUzUxMiJ9.eyJzdWIiOiIzMDYyMTgiLCJleHAiOjE1MjgxMjY3MDl9.w1tphX9J-frM7tbTK50v5qhhOI4uX0YX2wl1HtYTLIOWePjtGqOtm1avjYy-hEFLnCnFwPhDADI9Hp5hRNfuHQ
                        Accept-Encoding: br, gzip, deflate
                        Content-Type: application/x-www-form-urlencoded
                        Host: wallet.bbb-home.com
                      
                        body:
                        zuserId=156510&phone=13828855569&amount=15&password=111111&remark=&balanceId=9
                       response:
                        {
	                        "msg": "成功",
	                        "data": {},
	                        "retcode": 0
                        }
                      */
                     requestUrl = "https://wallet.bbb-home.com/app/balance/uddRollOut";

                     string tradePassword = "111111";
                     if(account.Password == "111111")
                     {
                         tradePassword = "222222";
                     }
                     requestBody = String.Format("zuserId=156510&phone=13828855569&amount={0}&password={1}&remark=&balanceId=9", assetTotal, tradePassword);
                     response = await SendRequestToBBM(token, requestUrl, requestBody);


                     obj = JObject.Parse(response);
                     string retcode = Convert.ToString(obj["retcode"]);
                     if(retcode != "0")
                     {
                         Log.WriteLog("Error: transfer failed :" + account.UserName + " \r\n");
                     }

                 }
            }
            catch(Exception ex)
            {
                Log.WriteLog(ex.ToString());
            }
        }

        private async void btnNetease_Click(object sender, EventArgs e)
        {
            await GetNeteaseVirCoins();
        }

        private async Task<string> GetNeteaseVirCoins()
        {
            string sessionid = null;

            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };

                string requestUrl = "https://star.8.163.com/api/home/index";
                var requestBody = "";
                string responseBody = "";
                //Cookie: NTES_YD_SESS=X7Z1aGKKxcUnvUrrgSqi.xRys7NR0Kr7JkYLiDGX5L8OAZ_WA7Kw6g7wQmo44jEz3e5CADylvrDuE8qCjZmB9Q04uYRVVr_7t4vASOGDxG9ryTwf6FQI4N1m57EkM_hZQW0ByTRRoiKQNpS50dM0yFn9.8e1bikRWeJnbYlYYZ1uFZnuNaMd0zE1PG5iQsh0IJJv0ici1paDQ3xOTUvJP5F7Of2WVCP_ULti09xeFp4V3; 
                // mp_MA-9E66-C87EFACB60BC_hubble=%7B%22sessionReferrer%22%3A%20%22https%3A%2F%2Fstar.8.163.com%2Fm%22%2C%22updatedTime%22%3A%201527868040006%2C%22sessionStartTime%22%3A%201527866967972%2C%22deviceUdid%22%3A%20%22f124f62b-3499-4cc1-b542-1f99567f2806%22%2C%22persistedTime%22%3A%201527858323703%2C%22LASTEVENT%22%3A%20%7B%22eventId%22%3A%20%22da_u_login%22%2C%22time%22%3A%201527868040007%7D%2C%22sessionUuid%22%3A%20%2286c12ada-b60b-4b07-b08f-dab9723b0257%22%2C%22user_id%22%3A%202043357%7D; 
                //_ga=GA1.2.1416979330.1527858324; 
                //_gat=1; 
                //NTES_YD_SESS=X7Z1aGKKxcUnvUrrgSqi.xRys7NR0Kr7JkYLiDGX5L8OAZ_WA7Kw6g7wQmo44jEz3e5CADylvrDuE8qCjZmB9Q04uYRVVr_7t4vASOGDxG9ryTwf6FQI4N1m57EkM_hZQW0ByTRRoiKQNpS50dM0yFn9.8e1bikRWeJnbYlYYZ1uFZnuNaMd0zE1PG5iQsh0IJJv0ici1paDQ3xOTUvJP5F7Of2WVCP_ULti09xeFp4V3; 
                //STAREIG=c87fd834cabbc3a981ab93848f5d4eb5ac61360d

                CookieContainer cookies = new CookieContainer();
                var ck = new Cookie("NTES_YD_SESS", "X7Z1aGKKxcUnvUrrgSqi.xRys7NR0Kr7JkYLiDGX5L8OAZ_WA7Kw6g7wQmo44jEz3e5CADylvrDuE8qCjZmB9Q04uYRVVr_7t4vASOGDxG9ryTwf6FQI4N1m57EkM_hZQW0ByTRRoiKQNpS50dM0yFn9.8e1bikRWeJnbYlYYZ1uFZnuNaMd0zE1PG5iQsh0IJJv0ici1paDQ3xOTUvJP5F7Of2WVCP_ULti09xeFp4V3");
                ck.Domain = "start.8.163.com";
                cookies.Add(ck);

                ck = new Cookie("mp_MA-9E66-C87EFACB60BC_hubble", "%7B%22sessionReferrer%22%3A%20%22https%3A%2F%2Fstar.8.163.com%2Fm%22%2C%22updatedTime%22%3A%201527868040006%2C%22sessionStartTime%22%3A%201527866967972%2C%22deviceUdid%22%3A%20%22f124f62b-3499-4cc1-b542-1f99567f2806%22%2C%22persistedTime%22%3A%201527858323703%2C%22LASTEVENT%22%3A%20%7B%22eventId%22%3A%20%22da_u_login%22%2C%22time%22%3A%201527868040007%7D%2C%22sessionUuid%22%3A%20%2286c12ada-b60b-4b07-b08f-dab9723b0257%22%2C%22user_id%22%3A%202043357%7D");
                ck.Domain = "start.8.163.com";
                cookies.Add(ck);

                ck = new Cookie("_ga", "GA1.2.1416979330.1527858324");
                ck.Domain = "start.8.163.com";
                cookies.Add(ck);
                
                ck = new Cookie("_gat", "1");
                ck.Domain = "start.8.163.com";
                cookies.Add(ck);

                ck = new Cookie("STAREIG", "c87fd834cabbc3a981ab93848f5d4eb5ac61360d");
                ck.Domain = "start.8.163.com";
                cookies.Add(ck);
                HttpClientHandler handler = new HttpClientHandler();
                handler.CookieContainer = cookies;

                HttpClient client = new HttpClient(handler);

                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");

                var task = client.SendAsync(requestMessage);

                await task.ContinueWith((requestTask) =>
                {
                    //requestTask.Result.Headers.GetValues("Set-Cookie")[1]
                    //"JSESSIONID=F192D31D7A03DDC79C1D84064EF0783D; Path=/; HttpOnly"


                    // Get HTTP response from completed task.
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                        //var header = requestTask.Result.Content.Headers.ToString();
                        //var t1 = requestTask.Result.Headers.GetValues("Set-Cookie");
                        responseBody = requestTask.Result.Content.ReadAsStringAsync().Result;
                        Uri uri = new Uri(requestUrl);
                        IEnumerable<Cookie> responseCookies = cookies.GetCookies(uri).Cast<Cookie>();
                        foreach (Cookie cookie in responseCookies)
                        {
                            Console.WriteLine(cookie.Name + ": " + cookie.Value);
                            if (cookie.Name == "JSESSIONID")
                            {
                                sessionid = cookie.Value;
                                break;
                            }
                        }

                    }

                });
            }
            catch (Exception ex)
            {
                Log.WriteLog(ex.ToString());
            }

            Log.WriteLog("Session Id: " + sessionid);
            return sessionid;
        }

        private async void btnNXH_Click(object sender, EventArgs e)
        {
            var api_server = "http://47.105.106.146:9002";
            int index = 0;
            IList<BBMAccounts.BBMAccount> nxhAccounts = new List<BBMAccounts.BBMAccount>();
            nxhAccounts.Add(new BBMAccounts.BBMAccount { UserName ="18688779580",Password="Office.1net"});
            foreach(var account in BBMAccounts.Accounts)
            {
                nxhAccounts.Add(account);
            }

            foreach (var account in nxhAccounts)
            {
                index++;
                JObject obj=null;
                string requestUrl = null;
                string requestBody = null;
                string result = null;
                string userName = account.UserName;
                string password = account.Password;
                string token = null;// "Bearer eyJhbGciOiJIUzUxMiJ9.eyJzdWIiOiIxODY4ODc3OTU4MCIsImF1ZGllbmNlIjoid2ViIiwiY3JlYXRlZCI6MTUyODYwNjIzOTA0OCwiZXhwIjoxNTI5MjExMDM5fQ.GXtDxgfqPwzRHd3BBkal-LgTJFOAFEmGYp1HBRSG5jEVwADjggHHZmOscbSrTl1gjo0fBQelMSvMGZ99nBz9aQ";
                // 目前有6个酒桶
                int barrelCount = 6;
                // 1.get token
                /*
    POST http://47.105.106.146:9002/jwtToken HTTP/1.1
    Host: 47.105.106.146:9002
    Connection: keep-alive
    Content-Length: 361
    Authorization: Bearer false, Basic YWRtaW46YWRtaW4=
    Origin: http://47.105.106.146
    User-Agent: Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/66.0.3359.181 Safari/537.36
    Content-Type: multipart/form-data; boundary=----WebKitFormBoundarySYE5xREaoirUSdie
    Referer: http://47.105.106.146/
    Accept-Encoding: gzip, deflate
    Accept-Language: en-US,en;q=0.9

    ------WebKitFormBoundarySYE5xREaoirUSdie
    Content-Disposition: form-data; name="username"

    18688779580
    ------WebKitFormBoundarySYE5xREaoirUSdie
    Content-Disposition: form-data; name="password"

    Office.1net
    ------WebKitFormBoundarySYE5xREaoirUSdie
    Content-Disposition: form-data; name="grant_type"

    password
    ------WebKitFormBoundarySYE5xREaoirUSdie--
                 */
       
                // 1.get token
                // everyday renew a token 0:0 - 0：30
                if(DateTime.Now.Hour < 1 && DateTime.Now.Minute <= 30 || String.IsNullOrWhiteSpace(account.Token))
                {
                    requestBody = "------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; name=\"username\"\r\n\r\n" + userName + "\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; name=\"password\"\r\n\r\n" + password + "\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; name=\"grant_type\"\r\n\r\npassword\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW--";
                    requestUrl = string.Format("{0}{1}", api_server, "/jwtToken");
                    //token = await SendRequestMultiForm(requestUrl, requestBody, "------WebKitFormBoundarySYE5xREaoirUSdie");

                    //token = "Bearer " + token;
                    var client = new RestClient(requestUrl);
                    var request = new RestRequest(Method.POST);
                    request.AddHeader("content-type", "multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW");
                    request.AddParameter("multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW", requestBody, ParameterType.RequestBody);
                    IRestResponse response = client.Execute(request);
                    var responseContent = response.Content;

                    Log.WriteLog(index + ". jwttoken api response for user " + userName + ":" + responseContent);

                    if (string.IsNullOrWhiteSpace(responseContent))
                    {
                        continue;
                    }
                    obj = JObject.Parse(responseContent);

                    this.txtResponse2.Text = "\n\r" + index + ".  " + DateTime.Now.ToString() + " User:" + userName + " response:" + responseContent + "\r\n" + this.txtResponse2.Text;

                    if (obj["token"] == null)
                    {
                        continue;
                    }

                    token = "Bearer " + obj["token"];
                    account.Token = token;
                }

                token = account.Token;
                try
                {
                    // 1. gather  -http://47.105.106.146:9002/v1/user/gather

                    requestUrl = string.Format("{0}{1}", api_server, "/v1/user/gather");
                    // 不需要做其他的动作，只要访问下api 即可
                    result = await SendRequestM(HttpMethod.Get, requestUrl, null, token, "");

                    // 如果没有gouqi，不需要继续
                    obj = JObject.Parse(result);
                    int gouqiTotal = Convert.ToInt32(obj["gouqiTotal"]);
                    if(gouqiTotal < 1)
                    {
                        continue;
                    }
                    // 2. charge
                    // http://47.105.106.146:9002/v1/user/charge/2
                    // Authorization: Bearer eyJhbGciOiJIUzUxMiJ9.eyJzdWIiOiIxODY4ODc3OTU4MCIsImF1ZGllbmNlIjoid2ViIiwiY3JlYXRlZCI6MTUyODYwNjIzOTA0OCwiZXhwIjoxNTI5MjExMDM5fQ.GXtDxgfqPwzRHd3BBkal-LgTJFOAFEmGYp1HBRSG5jEVwADjggHHZmOscbSrTl1gjo0fBQelMSvMGZ99nBz9aQ
                    // response:
                    // {"id":222,"username":"18688779580","password":"3cab93779736f48a707cfeb05ea2b8cc","type":1,"nickname":"Jackie","head":"1","nxh":4.202961,"gouqiPick":0,"gouqiTotal":76,"jiuping":9,"tong1Status":200,"tong1Timeout":1528614620226,"tong2Status":100,"tong2Timeout":0,"tong3Status":200,"tong3Timeout":1528626044844,"tong4Status":200,"tong4Timeout":1528614421647,"tong5Status":200,"tong5Timeout":1528626084754,"tong6Status":200,"tong6Timeout":1528626122077,"registeredat":1528260554422,"lastloginedat":1528606239044,"stircount":8,"stircoolingtime":1528637466661,"beginerNxh":0.202961,"basicNxh":0.659383,"inviteCode":"KQM"}

                    //2 charge 目前有6个酒桶
                    for (int i = 1; i <= barrelCount; i++)
                    {
                        requestUrl = string.Format("{0}{1}{2}", api_server, "/v1/user/charge/", i);
                        // 不需要做其他的动作，只要访问下api 即可
                        result = await SendRequestM(HttpMethod.Get, requestUrl, null, token, "");
                    }

                    // 3. brew --v1/user/brew/1

                    for (int i = 1; i <= barrelCount; i++)
                    {
                        requestUrl = string.Format("{0}{1}{2}", api_server, "/v1/user/brew/", i);
                        // 不需要做其他的动作，只要访问下api 即可
                        result = await SendRequestM(HttpMethod.Get, requestUrl, null, token, "");
                    }

                    //4. stir - http://47.105.106.146:9002/v1/user/stir
                    requestUrl = string.Format("{0}{1}", api_server, "/v1/user/stir");
                    result = await SendRequestM(HttpMethod.Get, requestUrl, null, token, "");



                }
                catch (Exception ex)
                {
                    this.txtResponse2.Text = "\n\r" + DateTime.Now.ToString() + " Error:" + ex.ToString() + "\n\r" + this.txtResponse2.Text;
                }
            }

            this.txtResponse2.Text = "\n\r============" + DateTime.Now.ToString() + " Run Completed.================" + this.txtResponse2.Text;
        }

        private void timer5min_Tick(object sender, EventArgs e)
        {
            Log.WriteLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + "5 mins timer starts \r\n");
            this.txtResponse2.Text = "\r\n============" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff") + "5 mins timer starts \r\n" + this.txtResponse2.Text;
            btnNXH_Click(null, null);
        }
        
    }
}
