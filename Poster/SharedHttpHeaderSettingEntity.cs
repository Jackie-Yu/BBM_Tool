using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Achievo.Poster
{
    public class SharedHttpHeaderSettingEntity
    {
        public string ContentType
        {
            get;
            set;
        }
        public string AppId
        {
            get;
            set;
        }

        public string AppSecret
        {
            get;
            set;
        }

        public string GetTokenRequestUrl
        {
            get;
            set;
        }

        public string GetTokenRequestBody
        {
            get;
            set;
        }

        public string AccessToken
        {
            get;
            set;
        }

        public string Agency
        {
            get;
            set;
        }

        public string Environment
        {
            get;
            set;
        }

        public string AccessKey
        {
            get;
            set;
        }

        public string Host
        {
            get;
            set;
        }
    }
}
