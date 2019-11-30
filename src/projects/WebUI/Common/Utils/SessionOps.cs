using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Common.Utils
{
    public class SessionOps
    {
        public static void SetSession(string key, object value, ISession session)
        {
            var serializedString = JsonConvert.SerializeObject(value);
            byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
            session.Set(key, encodedUserList);
        }
        public static List<T> GetSessionList<T>(byte[] encodedListFromSession)
        {
            if (encodedListFromSession != null)
            {
                string serializedString = Encoding.UTF8.GetString(encodedListFromSession);
                List<T> list = JsonConvert.DeserializeObject<List<T>>(serializedString);
                return list;
            }

            return null;
        }
        public static T GetSession<T>(byte[] encodedListFromSession)
        {
            if (encodedListFromSession != null)
            {
                string serializedString = Encoding.UTF8.GetString(encodedListFromSession);
                T re = JsonConvert.DeserializeObject<T>(serializedString);
                return re;
            }
            return default(T);
        }

        public static void AddSession<T>(string key, T value, byte[] encodedAllClaimListFromSession, ISession session)
        {
            List<T> list = new List<T>();
            if (encodedAllClaimListFromSession != null)
            {
                string deserializedString = Encoding.UTF8.GetString(encodedAllClaimListFromSession);
                list = JsonConvert.DeserializeObject<List<T>>(deserializedString);
                list.Add(value);
                var serializedString = JsonConvert.SerializeObject(list);
                byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                session.Set(key, encodedUserList);
            }
        }
        public static void RemoveSession<T>(string key, T value, byte[] encodedAllClaimListFromSession, ISession session)
        {
            List<T> list = new List<T>();
            if (encodedAllClaimListFromSession != null)
            {
                string deserializedString = Encoding.UTF8.GetString(encodedAllClaimListFromSession);
                list = JsonConvert.DeserializeObject<List<T>>(deserializedString);
                list.Remove(value);
                var serializedString = JsonConvert.SerializeObject(list);
                byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                session.Set(key, encodedUserList);
            }
        }
    }
}
