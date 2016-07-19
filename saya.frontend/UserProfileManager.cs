using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace saya.frontend
{
    class UserProfileManager
    {
        public UserProfile Profile { get; private set; }

        private string UserProfilePath = Path.Combine(
            Environment.GetEnvironmentVariable("APPDATA"),
            "saya",
            "UserProfile.xml");

        private void PrepareUserProfileDirectory()
        {
            var userProfileDirectory = Path.GetDirectoryName(UserProfilePath);
            if (!Directory.Exists(userProfileDirectory))
            {
                Directory.CreateDirectory(userProfileDirectory);
            }
        }

        public void Load()
        {
            if (File.Exists(UserProfilePath))
            {
                try
                {
                    using (var fs = new FileStream(UserProfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = System.Xml.XmlReader.Create(fs, new System.Xml.XmlReaderSettings
                    {
                        IgnoreComments = true,
                    }))
                    {
                        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(UserProfile), "");
                        Profile = (UserProfile)serializer.Deserialize(reader);
                    }
                    return;
                }
                catch (SystemException) { }
            }
            Profile = new UserProfile()
            {
                RecentlyUsedFilePath = new List<string>(),
            };
        }

        public void Save()
        {
            PrepareUserProfileDirectory();
            using (var writer = System.Xml.XmlWriter.Create(
                UserProfilePath,
                new System.Xml.XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                }))
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(UserProfile), "");
                var ns = new System.Xml.Serialization.XmlSerializerNamespaces();
                ns.Add("", "");
                serializer.Serialize(writer, Profile, ns);
            }
        }

    }
}
