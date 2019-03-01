﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;

namespace WebUI.Models
{
    public class Constants
    {
        public const string JsontagAdmin = "Admin";
        public const string JsontagUser = "User";
        public const string JsontagRole = "Role";
        public const string JsontagAuthorization = "Authorization";

        public static string[] RoleNames = { "Admin", "User", "Guest" };

        public const String MapEntry = "map";
        public const String PrefixEntry = "prefix";
        public static string[] AllCurrentTags = { MapEntry, PrefixEntry };
    }
}
