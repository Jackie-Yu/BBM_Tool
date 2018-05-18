using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace Achievo.Poster
{
    public class FileUtility
    {
        private static string HISTORY_DIRECTORY = "History";
        private static string HTTP_BASIC_SETTING_DIRECTORY = "HttpHeaderSettings";

        public static bool IsValidFileName(string fileName)
        {
            bool isValid = true;
            string errChar = "\\/:*?\"<>|";  
            if (string.IsNullOrWhiteSpace(fileName))
            {
                isValid = false;
            }
            else
            {
                for (int i = 0; i < errChar.Length; i++)
                {
                    if (fileName.Contains(errChar[i].ToString()))
                    {
                        isValid = false;
                        break;
                    }
                }
            }
            return isValid;
        }

        public static void WriteFile(string fileName, string content)
        {
            if (!System.IO.Directory.Exists(HISTORY_DIRECTORY))
            {
                System.IO.Directory.CreateDirectory(HISTORY_DIRECTORY);
            }
            Stream stream = File.Open(HISTORY_DIRECTORY + "\\" + fileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(stream);

            try
            {
                sw.WriteLine(content);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                sw.Close();
            }
        }

        public static void WriteHttpHeaderSettingFile(SharedHttpHeaderSettingEntity settingObj, HostEntity host)
        {
            if (!System.IO.Directory.Exists(HTTP_BASIC_SETTING_DIRECTORY))
            {
                System.IO.Directory.CreateDirectory(HTTP_BASIC_SETTING_DIRECTORY);
            }

            if (host == null) return;

            if (settingObj == null) return;

            string fileName = String.Format("{0}\\{1}_{2}", HTTP_BASIC_SETTING_DIRECTORY, host.Name, "httpHeadingSetting.txt").ToLower();

            //{content-Type}|||{appId}|||{appSecret}|||{GettingTokenRequestUrl}|||{GetttingTokenRequestBody}|||{AccessToken}|||{agency}|||{environment}|||{accesskey}
            string pattern = "{0}|||{1}|||{2}|||{3}|||{4}|||{5}|||{6}|||{7}|||{8}";
            string content = String.Format(pattern, settingObj.ContentType, 
                settingObj.AppId, settingObj.AppSecret, 
                settingObj.GetTokenRequestUrl, settingObj.GetTokenRequestBody, settingObj.AccessToken,
                settingObj.Agency,settingObj.Environment,settingObj.AccessKey);
            Stream stream = File.Open(fileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(stream);

            try
            {
                sw.WriteLine(content);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                sw.Close();
            }
        }

        public static SharedHttpHeaderSettingEntity ReadHttpHeaderSettingFile(HostEntity host)
        {
            if (!System.IO.Directory.Exists(HTTP_BASIC_SETTING_DIRECTORY))
            {
                return null;
            }
            if (host == null) return null;

            string fileName =String.Format("{0}\\{1}_{2}", HTTP_BASIC_SETTING_DIRECTORY,host.Name,"httpHeadingSetting.txt");
            if (!File.Exists(fileName))
            {
                return null;
            }
            try
            {
                StreamReader sr = new StreamReader(fileName);
                string content = sr.ReadToEnd();
                sr.Close();
                
                if(String.IsNullOrWhiteSpace(content))
                {
                    return null;
                }

                content = content.Replace("\r\n", "");

                var segments = content.Split(new string[]{"|||"},StringSplitOptions.None);
                //if(segments.Length != 9)
                //{
                //    return null;
                //}
                //{content-Type}|||{appId}|||{appSecret}|||{GettingTokenRequestUrl}|||{GetttingTokenRequestBody}|||{AccessToken}|||{agency}|||{environment}|||{accesskey}
                //string pattern = "{0}|||{1}|||{2}|||{3}|||{4}|||{5}|||{6}|||{7}|||{8}";
                SharedHttpHeaderSettingEntity settingEntity = new SharedHttpHeaderSettingEntity();

                if (segments.Length == 0) return null;

                settingEntity.ContentType = segments[0];

                if(segments.Length > 1)
                settingEntity.AppId = segments[1];

                if (segments.Length > 2)
                settingEntity.AppSecret = segments[2];

                if (segments.Length > 3)
                settingEntity.GetTokenRequestUrl = segments[3];

                if (segments.Length > 4)
                settingEntity.GetTokenRequestBody = segments[4];

                if (segments.Length > 5)
                settingEntity.AccessToken = segments[5];

                if (segments.Length > 6)
                settingEntity.Agency = segments[6];

                if (segments.Length > 7)
                settingEntity.Environment = segments[7];

                if (segments.Length > 8)
                settingEntity.AccessKey = segments[8];

                return settingEntity;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void WriteHostsToFile(List<HostEntity> hosts)
        {
            if (!System.IO.Directory.Exists(HTTP_BASIC_SETTING_DIRECTORY))
            {
                System.IO.Directory.CreateDirectory(HTTP_BASIC_SETTING_DIRECTORY);
            }

            string content = XmlConverter.ToXml(hosts);
            string fileName = HTTP_BASIC_SETTING_DIRECTORY + "\\hosts.txt";

            Stream stream = File.Open(fileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(stream);

            try
            {
                sw.WriteLine(content);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                sw.Close();
            }

        }

        public static List<HostEntity> ReadHostsFromFile()
        {
            List<HostEntity> result = new List<HostEntity>();

            if (!System.IO.Directory.Exists(HTTP_BASIC_SETTING_DIRECTORY))
            {
                return null;
            }

            string fileName = HTTP_BASIC_SETTING_DIRECTORY + "\\hosts.txt";
            if (!File.Exists(fileName))
            {
                return null;
            }

            try
            {
                StreamReader sr = new StreamReader(fileName);
                string content = sr.ReadToEnd();
                sr.Close();

                if (String.IsNullOrWhiteSpace(content))
                {
                    return result;
                }

                result = XmlConverter.FromXmlTo<List<HostEntity>>(content);

                return result;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void DeleteHistory(string displayName)
        {
            string fileName = Application.StartupPath + "\\" + HISTORY_DIRECTORY + "\\" + displayName + ".txt";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }

        public static RequestEntity GetRequestDataFromFile(string nameWithoutSuffix)
        {
            if (!System.IO.Directory.Exists(HISTORY_DIRECTORY))
            {
                return null;
            }

            string fileName = HISTORY_DIRECTORY + "\\" + nameWithoutSuffix + ".txt";
            if(!File.Exists(fileName))
            {
                return null;
            }
            try
            {
                RequestEntity request = new RequestEntity();
                StreamReader sr = new StreamReader(fileName);
                string content = sr.ReadToEnd();
                sr.Close();
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
                string flagOfRequestUrl = "----------RequestURL----------";
                string flagOfRequestHeader = "----------RequestHeader----------";
                string flagOfRequestBody = "----------RequestBody----------";
                // request Url
                int start = content.IndexOf(flagOfRequestUrl) + flagOfRequestUrl.Length;
                int end = content.IndexOf(flagOfRequestUrl, start);
                request.RequestURL = content.Substring(start, end - start - 1);
                //request.RequestURL = request.RequestURL.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
                int position = request.RequestURL.IndexOf("=") + 1;
                request.RequestURL = request.RequestURL.Substring(position,request.RequestURL.Length - position);
                // request Header
                start = content.IndexOf(flagOfRequestHeader) + flagOfRequestHeader.Length;
                end = content.IndexOf(flagOfRequestHeader, start);
                request.RequestHeader = content.Substring(start, end - start - 1);
                //request.RequestHeader = request.RequestHeader.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
                position = request.RequestHeader.IndexOf("=") + 1;
                request.RequestHeader = request.RequestHeader.Substring(position, request.RequestHeader.Length - position);
                // request body
                start = content.IndexOf(flagOfRequestBody) + flagOfRequestBody.Length;
                end = content.IndexOf(flagOfRequestBody, start);
                request.RequestBody = content.Substring(start, end - start - 1);
                //request.RequestBody = request.RequestBody.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
                position = request.RequestBody.IndexOf("=") + 1;
                request.RequestBody = request.RequestBody.Substring(position, request.RequestBody.Length - position);
                return request;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static FileInfo[] GetHistoryList()
        {
            if (!Directory.Exists(HISTORY_DIRECTORY))
            {
                return null;
            }

            DirectoryInfo di = new DirectoryInfo(HISTORY_DIRECTORY);
            FileInfo[] files = di.GetFiles();

            return files;
        }
    }
}
