using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Linq;
using System.Media;

class rubika
{
    private byte[] iv = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    private AesCryptoServiceProvider AesSet;
    private ICryptoTransform crp;
    private List<MessageData> msgs = new List<MessageData>();
    private String api_url = "https://messengerg2c63.iranlms.ir";
    private String auth;
    public rubika(String str)
    {
        auth = str;
        String str2 = str.Substring(16).Substring(0, 8) + str.Substring(0, 8) + str.Substring(24) + str.Substring(8).Substring(0, 8);
        char[] sb = str2.ToCharArray();


        for (int i2 = 0; i2 < sb.Length; i2++)
        {
            if (sb[i2] >= '0' && sb[i2] <= '9')
            {
                sb[i2] = (char)((((str2[i2] - '0') + 5) % 10) + 48);
            }
            if (sb[i2] >= 'a' && sb[i2] <= 'z')
            {
                sb[i2] = (char)((((str2[i2] - 'a') + 9) % 26) + 97);
            }
        }

        AesSet = new AesCryptoServiceProvider();
        AesSet.BlockSize = 128;
        AesSet.KeySize = 256;
        AesSet.Key = ASCIIEncoding.ASCII.GetBytes(new String(sb));
        AesSet.IV = iv;
        AesSet.Padding = PaddingMode.PKCS7;
        AesSet.Mode = CipherMode.CBC;
    }

    // ------------- update message --------------
    public class MessageData
    {
        public double id;
        public String text;
        public double replay;
        public String sender_token;
        public string type;
    }
    Action<MessageData> cback;
    String gptn;

    public void createBOT(Action<MessageData> callback, String group_token)
    {
        this.cback = callback;
        gptn = group_token;
        if (get_main_id(gptn) == 0)
        {
            Console.WriteLine("Error");
            return;
        }
        
        string handShake = "{\"api_version\":\"5\",\"auth\":\"" + auth + "\",\"data\":\"\",\"method\":\"handShake\"}";
        Socket socket = new Socket();
        socket.connect("wss://jsocket5.iranlms.ir:80");
        while (true)
        {
            Thread.Sleep(1000);
            if (socket.getState() == WebSocketState.Open)
            {
                Console.WriteLine("connected !" + "\n");
                break;
            }
        }
        socket.createReader(receiver);
        socket.send(handShake);
    }

    private void receiver(string rcvData)
    {
        JObject data = JObject.Parse(rcvData);
        JObject j = null;
        Boolean isUpdate = false;
        if (data.ContainsKey("data_enc"))
        {
            j = JObject.Parse(crypto(data.SelectToken("data_enc").ToString(), true));

        }else if (data.ContainsKey("messenger"))
        {
            j = JObject.Parse(data.SelectToken("messenger").ToString());
        }else
        {
            Console.WriteLine(rcvData);
        }

        if (j!= null && j.ContainsKey("message_updates"))
        {
            JArray n = JArray.Parse(j.SelectToken("message_updates").ToString());
            for (int i = 0; i < n.Count; i++)
            {
                if (n[i].SelectToken("object_guid").ToString() == gptn)
                {
                    isUpdate = true;
                }
            }
        }
        if (j != null && j.ContainsKey("chat_updates"))
        {
            JArray n = JArray.Parse(j.SelectToken("chat_updates").ToString());
            for (int i = 0; i < n.Count; i++)
            {
                if (n[i].SelectToken("object_guid").ToString() == gptn)
                {
                    isUpdate = true;
                }
            }
        }
        if (isUpdate)
        {
            update();
        }
    }
    private void update()
    {
        double main_id = msgs[msgs.Count - 1].id;
        
        MessageData[] new_msgs = getNewMsg(main_id);

        if (new_msgs != null)
        {
            for (int i = 0; i < new_msgs.Length; i++)
            {
                if (new_msgs[i].id != msgs[msgs.Count - 1].id)
                {
                    cback(new_msgs[i]);
                    msgs.Add(new_msgs[i]);
                }
            }
        }

    }
    private MessageData[] getNewMsg(double main_id)
    {
        try
        {
            JObject js = new JObject();
            js.Add("limit", 10);
            js.Add("min_id", main_id);
            js.Add("object_guid", gptn);
            js.Add("sort", "FromMin");


            List<MessageData> m = new List<MessageData>();
            JObject data = JObject.Parse(crypto(JObject.Parse(send_request(api_url, getbytes(makeData4(js.ToString(), "getMessages")))).SelectToken("data_enc").ToString(), true));
            JArray updatmsg = JArray.Parse(data.SelectToken("messages").ToString());
            for (int i = 0; i < updatmsg.Count; i++)
            {
                JObject msg = JObject.Parse(updatmsg[i].ToString());
                MessageData new_msg = new MessageData();
                new_msg.type = msg.SelectToken("type").ToString();
                if (msg.ContainsKey("text"))
                {
                    new_msg.text = msg.SelectToken("text").ToString();
                }
                if (msg.ContainsKey("reply_to_message_id"))
                {
                    new_msg.replay = double.Parse(msg.SelectToken("reply_to_message_id").ToString());
                }
                new_msg.id = double.Parse(msg.SelectToken("message_id").ToString());
                if (new_msg.type != "Event")
                {
                    new_msg.sender_token = msg.SelectToken("author_object_guid").ToString();
                }
                m.Add(new_msg);
            }



            return m.ToArray();
        }catch (Exception ex) { return null; }
    }

    private double get_main_id(String t)
    {
        try
        {
            JObject data = JObject.Parse(crypto(JObject.Parse(send_request(api_url, getbytes(makeData4("{\"group_guid\":\"" + t + "\"}", "getGroupInfo")))).SelectToken("data_enc").ToString(), true));
            double _id = double.Parse(data.SelectToken("chat").SelectToken("last_message").SelectToken("message_id").ToString());
            msgs.Add(new MessageData() { id = _id });
            return _id;
        }
        catch(Exception e) { return 0; }
    }



    // ----------------- control method`s -----------------

    public class user_data
    {
        public String name;
        public String lastName;
        public String bio;
        public String username;
    }

    public void update_profile(user_data data)
    {
        if (data.bio != null || data.lastName != null || data.name != null)
        {
            JObject js = new JObject();
            JArray ja = new JArray();
            if (data.name != null)
            {
                js.Add("first_name", data.name);
                ja.Add("first_name");
            }
            if (data.lastName != null)
            {
                js.Add("last_name", data.lastName);
                ja.Add("last_name");
            }
            if (data.bio != null)
            {
                js.Add("bio", data.bio);
                ja.Add("bio");
            }
            js.Add("updated_parameters", ja);
            send_request(api_url, getbytes(makeData4(js.ToString(), "updateProfile")));
        }


        if (data.username != null)
        {
            send_request(api_url, getbytes(makeData4("{\"username\":\"" + data.username + "\"}", "updateUsername")));
        }
    }

    public user_data get_user_info(String userTOKEN)
    {
        try{
            JObject data = JObject.Parse(crypto(JObject.Parse(send_request(api_url, getbytes(makeData4("{\"user_guid\":\"" + userTOKEN + "\"}", "getUserInfo")))).SelectToken("data_enc").ToString(), true));
            user_data userData = new user_data();
            userData.name = data.SelectToken("user").SelectToken("first_name").ToString();
            userData.lastName = data.SelectToken("user").SelectToken("last_name").ToString();
            userData.bio = data.SelectToken("user").SelectToken("bio").ToString();
            userData.username = data.SelectToken("user").SelectToken("username").ToString();
            return userData;
        }catch(Exception e){ return null; }
    }

    public String get_id_token(String id)
    {
        try
        {
            JObject s = JObject.Parse(crypto(JObject.Parse(send_request(api_url, getbytes(makeData4("{\"username\":\"" + id + "\"}", "getObjectByUsername")))).SelectToken("data_enc").ToString(), true));
            return s.ToString();
            return s.SelectToken("user").SelectToken("user_guid").ToString();
        }catch { return null; }

    }

    public void delete_message(String msgID, String chat_token)
    {
        send_request(api_url, getbytes(makeData4("{\"message_ids\":[" + msgID + "],\"object_guid\":\"" + chat_token + "\",\"type\":\"Global\"}", "deleteMessages")));
    }

    public void send_message(String text, String chat_token)
    {
        JObject js = new JObject();
        js.Add("is_mute", false);
        js.Add("object_guid", chat_token);
        js.Add("rnd", new Random().Next(100000000, 999999999));
        js.Add("text", text);
        String data = makeData4(js.ToString(), "sendMessage");
        send_request(api_url, getbytes(data));
    }

    public string send_mention_text(String chat_token, String text, int[][] data, string[] tokens)
    {

        JObject js = new JObject();
        js.Add("is_mute", false);
        js.Add("object_guid", chat_token);
        js.Add("rnd", new Random().Next(100000000, 999999999));
        js.Add("text", text);
        
        JArray meta_data = new JArray();

        for (int i = 0;i< tokens.Length; i++)
        {
            meta_data.Add(JObject.Parse("{\"from_index\":"+data[i][0]+",\"length\":"+data[i][1]+",\"mention_text_object_guid\":\""+tokens[i]+"\",\"type\":\"MentionText\"}"));

        }

        js.Add("metadata", JObject.Parse("{ \"meta_data_parts\": "+meta_data+"}"));
        Console.WriteLine(js+"\n\n");
        return send_request(api_url, getbytes(makeData4(js.ToString(), "sendMessage")));


    }

    public void send_replay(String text, double replay_msg_id, String chat_token)
    {
        JObject js = new JObject();
        js.Add("is_mute", false);
        js.Add("object_guid", chat_token);
        js.Add("rnd", new Random().Next(100000000, 999999999));
        js.Add("text", text);
        js.Add("reply_to_message_id", replay_msg_id);
        String data = makeData4(js.ToString(), "sendMessage");
        send_request(api_url, getbytes(data));
    }

    public void edit_message(String new_text, double msg_id, String chat_token)
    {
        JObject js = new JObject();
        js.Add("object_guid", chat_token);
        js.Add("text", new_text);
        js.Add("message_id", msg_id);
        String data = makeData4(js.ToString(), "editMessage");
        send_request(api_url, getbytes(data));
    }

    public void send_location(String chat_token, double x, double y)
    {
        JObject js = new JObject();
        js.Add("is_mute", false);
        js.Add("object_guid", chat_token);
        js.Add("rnd", new Random().Next(100000000, 999999999));
        js.Add("location", JObject.Parse("{\"latitude\":" + x + ",\"longitude\":" + y + "}"));
        String data = makeData4(js.ToString(), "sendMessage");
        send_request(api_url, getbytes(data));
    }



    public MessageData getMessageById(double msg_id, String chat_token)
    {
        JObject s = JObject.Parse(JObject.Parse(crypto(JObject.Parse(send_request(api_url, getbytes(makeData4("{\"message_ids\":[\"" + msg_id + "\"],\"object_guid\":\"" + chat_token + "\"}", "getMessagesByID")))).SelectToken("data_enc").ToString(), true)).SelectToken("messages")[0].ToString());
        
        MessageData data = new MessageData();
        Console.WriteLine(s);
        if (s.ContainsKey("text"))
        {
            data.text = s.SelectToken("text").ToString();
        }
        Console.WriteLine("1");
        if (s.ContainsKey("reply_to_message_id"))
        {
            data.replay = double.Parse(s.SelectToken("reply_to_message_id").ToString());
        }
        Console.WriteLine("1");
        data.id = double.Parse(s.SelectToken("message_id").ToString());
        Console.WriteLine("1");
        data.sender_token = s.SelectToken("author_object_guid").ToString();
        Console.WriteLine("1");
        return data;
    }

    public void Remove_user(String group_token, String user_token)
    {
        JObject js = new JObject();
        js.Add("action", "Set");
        js.Add("group_guid", group_token);
        js.Add("member_guid", user_token);
        String data = makeData5(js.ToString(), "banGroupMember");
        send_request(api_url, getbytes(data));
    }

    public void UnRemove_user(String group_token, String user_token)
    {
        JObject js = new JObject();
        js.Add("action", "Unset");
        js.Add("group_guid", group_token);
        js.Add("member_guid", user_token);
        String data = makeData5(js.ToString(), "banGroupMember");
        send_request(api_url, getbytes(data));
    }

    class admin_access
    {
        String Set_Member_Access = "SetMemberAccess";
        String Set_Join_Link = "SetJoinLink";
        String Pin_Messages = "PinMessages";
        String Set_Admin = "SetAdmin";
        String Ban_Member = "BanMember";
        String Delete_Messages = "DeleteGlobalAllMessages";
        String Change_Info = "ChangeInfo";
    }

    public void add_admin(String group_token, String user_token, String[] access)
    {
        JArray access_list = new JArray();

        foreach (String s in access)
        {
            access_list.Add(s);
        }

        JObject js = new JObject();
        js.Add("access_list", access_list);
        js.Add("action", "SetAdmin");
        js.Add("group_guid", group_token);
        js.Add("member_guid", user_token);
        String data = makeData5(js.ToString(), "setGroupAdmin");
        send_request(api_url, getbytes(data));
    }

    public void delete_admin(String group_token, String admin_token)
    {
        JObject js = new JObject();
        js.Add("action", "UnsetAdmin");
        js.Add("group_guid", group_token);
        js.Add("member_guid", admin_token);
        String data = makeData5(js.ToString(), "setGroupAdmin");
        send_request(api_url, getbytes(data));
    }



    public void change_group_timer(String group_token, int time)
    {
        send_request(api_url, getbytes(makeData4("{\"group_guid\":\"" + group_token + "\",\"slow_mode\":" + time + ",\"updated_parameters\":[\"slow_mode\"]}", "editGroupInfo")));
    }

    public void change_group_link(String group_token)
    {
        send_request(api_url, getbytes(makeData4("{\"group_guid\":\"" + group_token + "\"}", "setGroupLink")));
    }

    public String get_group_link(String group_token)
    {
        try
        {
            JObject res = JObject.Parse(crypto(JObject.Parse(send_request(api_url, getbytes(makeData4("{\"group_guid\":\"" + group_token + "\"}", "getGroupLink")))).SelectToken("data_enc").ToString(), true));
            return res.SelectToken("join_link").ToString().Replace("\\", "");
        }catch { return null; }

    }



    public String makeData5(String data, String method)
    {
        JObject js = new JObject();
        js.Add("client", JObject.Parse("{\"app_name\":\"Main\",\"app_version\":\"2.8.1\",\"lang_code\":\"fa\",\"package\":\"ir.resaneh1.iptv\",\"platform\":\"Android\"}"));
        js.Add("input", JObject.Parse(data));
        js.Add("method", method);

        String data_enc = crypto(js.ToString(), false);
        js.RemoveAll();
        js.Add("api_version", "5");
        js.Add("auth", auth);
        js.Add("data_enc", data_enc);

        return js.ToString();
    }
    public String makeData4(String data, String method)
    {
        JObject js = new JObject();
        js.Add("api_version", "4");
        js.Add("auth", auth);
        js.Add("client", JObject.Parse("{\"app_name\":\"Main\",\"app_version\":\"2.8.1\",\"lang_code\":\"fa\",\"package\":\"ir.resaneh1.iptv\",\"platform\":\"Android\"}"));
        js.Add("data_enc", crypto(data, false));
        js.Add("method", method);
        return js.ToString();
    }




    //---------- tools -----------
    private String send_request(String url, byte[] data)
    {
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json; charset=UTF-8";
            request.ContentLength = data.Length;
            request.GetRequestStream().Write(data, 0, data.Length);
            return new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();
        }
        catch { return null; }

    }

    private class Socket
    {
        CancellationTokenSource cts;
        ClientWebSocket client;
        public Socket()
        {
            client = new ClientWebSocket();
            cts = new CancellationTokenSource();
        }
        public void createReader(Action<string> callback)
        {
            Task.Factory.StartNew(
                async () =>
                {
                    Byte[] msize = new byte[99999];
                    ArraySegment<byte> Buffer = new ArraySegment<byte>(msize);
                    while (true)
                    {
                        WebSocketReceiveResult rcvResult = await client.ReceiveAsync(Buffer, cts.Token);
                        callback(Encoding.UTF8.GetString(Buffer.Skip(Buffer.Offset).Take(rcvResult.Count).ToArray()));
                    }
                }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        public void send(String msg)
        {

            client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, cts.Token);
            Console.WriteLine(msg);
        }
        public void connect(String soc_url)
        {
            client.ConnectAsync(new Uri(soc_url), cts.Token);
        }
        public WebSocketState getState()
        {
            return client.State;
        }
    }

    private byte[] getbytes(string d)
    {
        return Encoding.UTF8.GetBytes(d);
    }


    private string crypto(String data, bool b)
    {
        if (b)
        {
            data = data.Replace("\\n", "\n");
            crp = AesSet.CreateDecryptor(AesSet.Key, AesSet.IV);
            return Encoding.UTF8.GetString(crp.TransformFinalBlock(Convert.FromBase64String(data), 0, Convert.FromBase64String(data).Length));
        }
        else
        {
            byte[] f = getbytes(data);
            crp = AesSet.CreateEncryptor(AesSet.Key, AesSet.IV);
            return Convert.ToBase64String(crp.TransformFinalBlock(f, 0, f.Length));
        }
    }

}