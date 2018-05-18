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

namespace Achievo.Poster
{
    public partial class PostForm : Form
    {
        private long elapsedTime = 0;
        private string costFormat = "Response - Costs: {0} ms";
        private const string LINE_SEPARATE = "\n\r=====================================[{0}], Costs:{1} ms, Length:{2} ===================================\n\r";
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
                NameValueCollection headers = this.GetHeaders();
                string requestBody = this.txtRequestBody.Text.Trim();

                Task[] tasks = new Task[concurrent];
                for (int i = 0; i < concurrent; i++)
                {
                    int index = i + 1;

                    tasks[i] = Task.Run(async () => await SendRequest(httpMethod, headers, index, concurrent, url, requestBody));
                }

                //// start task
                await Task.WhenAll(tasks);

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

        private async Task SendRequest(HttpMethod httpMethod, NameValueCollection headers, int sequence, int total, string requestUrl, string requestBody = null)
        {
            requestUrl = requestUrl.Replace(" ", "").Replace("\\r", "").Replace("\\n", "").Replace("\\r\\n", "");

            try
            {

                ServicePointManager.ServerCertificateValidationCallback = delegate(Object obj, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) { return (true); };
                HttpClient client = new HttpClient();
                HttpRequestMessage requestMessage = new HttpRequestMessage(httpMethod, requestUrl);

                if (httpMethod != HttpMethod.Get)
                {
                    requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, headers["Content-Type"]);
                }

                SetRequestHeader(client, requestMessage, headers);
                    
                string responseString = string.Empty;

                elapsedTime = 0;
                Stopwatch watch = new Stopwatch();
                watch.Start();
                TaskScheduler uiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

                var task = client.SendAsync(requestMessage);
                       
                await task.ContinueWith((requestTask) =>
                {
                    if (this.txtResponse.Text == "Processing...")
                    {
                        this.txtResponse.Text = "Results :";
                    }
                    // Get HTTP response from completed task.
                    if (requestTask.IsCompleted && requestTask.Status == TaskStatus.RanToCompletion)
                    {
                    //    HttpResponseMessage response = requestTask.Result;
                        var header = requestTask.Result.Content.Headers.ToString();
                        this.txtResponseHeader.Text = ((int)requestTask.Result.StatusCode) + "-" + requestTask.Result.StatusCode.ToString() + "\r\n" + requestTask.Result.Headers.ToString();
                        //response.Content.Headers.ContentType
                        responseString = requestTask.Result.Content.ReadAsStringAsync().Result;
                        watch.Stop();
                        elapsedTime = watch.ElapsedMilliseconds;
                        this.txtResponse.Text += String.Format(LINE_SEPARATE, sequence, elapsedTime, responseString.Length) + responseString;
                        groupResponse.Text = String.Format(costFormat, elapsedTime);
                        ListViewItem item = new ListViewItem(new string[] { sequence.ToString(), elapsedTime.ToString() });
                        listView1.Items.Add(item);
                    }
                    else if (requestTask.Status == TaskStatus.Faulted)
                    {
                        this.txtResponse.Text += String.Format(LINE_SEPARATE, sequence, elapsedTime, responseString.Length);
                        this.txtResponse.Text += "Request failed. Server Exception: \r\n" + requestTask.Exception.ToString();
                    }
                    else
                    {
                        this.txtResponse.Text += String.Format(LINE_SEPARATE, sequence, elapsedTime, responseString.Length);
                        this.txtResponse.Text += "Request Failed, please check the request url.";
                    }
                }, uiTaskScheduler);
            }
            catch (Exception ex)
            {
                this.txtResponse.Text += String.Format(LINE_SEPARATE, sequence, elapsedTime, 0);
                this.txtResponse.Text += ex.Message;
            }
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
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(headers[key]));
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
        private NameValueCollection GetHeaders()
        {
            NameValueCollection headerList = new NameValueCollection();

            string headers = this.txtRequestHeader.Text.Trim();

            string[] arr = headers.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in arr)
            {
                if (!String.IsNullOrWhiteSpace(s))
                {
                    string[] kv = s.Split(new char[] { ':' }, 2,StringSplitOptions.RemoveEmptyEntries);
                    if (kv.Length > 1)
                    {
                        string key = kv[0].Trim();
                        if (!key.StartsWith("//"))
                        {
                            headerList.Add(key, kv[1].Trim());
                        }
                    }
                }
            }

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
                    this.txtRequestUrl.Text = re.RequestURL.Replace("\r","");
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
    }
}
