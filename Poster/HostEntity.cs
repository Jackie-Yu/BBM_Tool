using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Achievo.Poster
{
    [Serializable]
    public class HostEntity
    {
        public string Name { get; set; }
        public string HostUrl { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

}
