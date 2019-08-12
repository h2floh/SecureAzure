using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AADLoginSamplePersonalAndAllOrg.Infrastructure
{
    /// <summary>
    /// Contains a list of all the Azure Ad app roles this app works with
    /// </summary>
    public static class AppRoles
    {
        public const string Readers = "read";
        public const string Admins = "write";
    }
}
