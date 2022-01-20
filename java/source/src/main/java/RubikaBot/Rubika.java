package RubikaBot;

import android.util.Base64;
import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URI;
import java.net.URL;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;
import javax.crypto.Cipher;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;
import org.json.JSONObject;
import org.json.JSONArray;

import tech.gusavila92.websocketclient.WebSocketClient;

public class Rubika{
    private IvParameterSpec iv_aes = new IvParameterSpec(new byte[]{0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0});
    private SecretKeySpec key_aes;
    private List<MessageData> msgs = new ArrayList<>();
    private String api_url = "https://messengerg2c6.iranlms.ir";
    private String auth;
    
    public Rubika(String auth){
        this.auth = auth;
        String subString = auth.substring(0, 8);
        String subString2 = auth.substring(8, 16);
        String str2 = auth.substring(16, 24) + subString + auth.substring(24, 32) + subString2;
        StringBuilder sb = new StringBuilder(str2);
        for (int i2 = 0; i2 < sb.length(); i2++) {
            if (sb.charAt(i2) >= '0' && sb.charAt(i2) <= '9') {
                sb.setCharAt(i2, (char) ((((str2.charAt(i2) - '0') + 5) % 10) + 48));
            }
            if (sb.charAt(i2) >= 'a' && sb.charAt(i2) <= 'z') {
                sb.setCharAt(i2, (char) ((((str2.charAt(i2) - 'a') + 9) % 26) + 97));
            }
        }
        key_aes = new SecretKeySpec(sb.toString().getBytes(), "AES");
    }

    // ------------- update message --------------
    public class MessageData{
        public JSONObject json;
        public long id;
        public String text;
        public long replay;
        public String sender_token;
        public String type;
    }
    
    public interface listener{
        void onMessage(MessageData data);
        void onError();
    }

    private listener _callback;
    private String gptn;

    public void createBOT(final listener callback, String group_token){
        this._callback = callback;
        gptn = group_token;
        if (get_main_id(gptn) == 0)
        {
            callback.onError();
            return;
        }
        String handShake = "{\"api_version\":\"5\",\"auth\":\"" + auth + "\",\"data\":\"\",\"method\":\"handShake\"}";
        try {
            WebSocketClient ws;
            ws = new WebSocketClient(new URI("wss://jsocket3.iranlms.ir:80/")) {

                @Override
                public void onOpen() {}

                @Override
                public void onTextReceived(String message) {
                    try {
                        receive(message);
                    }catch (Exception e){}

                }

                @Override
                public void onBinaryReceived(byte[] data) {

                }

                @Override
                public void onPingReceived(byte[] data) {
                }

                @Override
                public void onPongReceived(byte[] data) {

                }

                @Override
                public void onException(Exception e) {
                    callback.onError();
                }

                @Override
                public void onCloseReceived() {
                    callback.onError();
                }
            };
            ws.setConnectTimeout(10000);
            ws.setReadTimeout(60000);
            ws.connect();
            ws.send(handShake);
        } catch (Exception e1) {
            callback.onError();
        }
        
    }
    private void receive(String rcvData) throws Exception {
        JSONObject data = new JSONObject(rcvData);
        JSONObject j = null;
        Boolean isUpdate = false;
        if (data.has("data_enc"))
        {
            j = new JSONObject(crypto(data.getString("data_enc"), true));
        }else if (data.has("messenger"))
        {
            j = data.getJSONObject("messenger");
        }
        if (j != null && j.has("message_updates")){
            JSONArray n = j.getJSONArray("message_updates");
            for (int i = 0; i < n.length(); i++)
            {
                if (n.getJSONObject(i).getString("object_guid").equals(gptn))
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
    private void update(){
        long main_id = msgs.get(msgs.size() - 1).id;
        MessageData[] new_msgs = getNewMsg(main_id);
        if (new_msgs != null)
        {
            for (int i = 0; i < new_msgs.length ; i++)
            {
                if (new_msgs[i].id != msgs.get(msgs.size()-1).id)
                {
                    _callback.onMessage(new_msgs[i]);
                    msgs.add(new_msgs[i]);
                }
            }
        }

    }
    private MessageData[] getNewMsg(Long main_id){
        try {
            JSONObject js = new JSONObject();
            js.put("limit", 10);
            js.put("min_id", main_id);
            js.put("object_guid", gptn);
            js.put("sort", "FromMin");

            JSONObject data = new JSONObject(crypto(new JSONObject(send_request(api_url, getbytes(makeData4(js.toString(), "getMessages")))).getString("data_enc"), true));
            JSONArray updatmsg = data.getJSONArray("messages");
            MessageData[] m = new MessageData[updatmsg.length()];
            for (int i = 0; i < updatmsg.length() ; i++)
            {
                JSONObject msg = updatmsg.getJSONObject(i);
                MessageData new_msg = new MessageData();
                new_msg.type = msg.getString("type"); 

                if (msg.has("text"))
                {
                    new_msg.text = msg.getString("text");

                }
                if (msg.has("reply_to_message_id"))
                {
                    new_msg.replay = msg.getLong("reply_to_message_id");

                }
                new_msg.id = msg.getLong("message_id");

                if (new_msg.type != "Event")
                {
                    new_msg.sender_token = msg.getString("author_object_guid");

                }

                m[i] = new_msg;
            }
            return m;
        }catch (Exception ex) {return null;}
    }
    private long get_main_id(String t){
        try{
            String ss = send_request(api_url, getbytes(makeData4("{\"group_guid\":\"" + t + "\"}", "getGroupInfo")));
            String data_dec = crypto(new JSONObject(ss).getString("data_enc"), true);
            long _id = new JSONObject(data_dec).getJSONObject("chat").getJSONObject("last_message").getLong("message_id");
            MessageData s = new MessageData();
            s.id = _id;
            msgs.add(s);
            return _id;
        }catch(Exception e){return 0;}
    }

    // ----------------- control method`s -----------------

    public class user_data {
        public String name;
        public String lastName;
        public String bio;
        public String username;
    }

    public void update_profile(user_data data){
        try{
            if (data.bio != null || data.lastName != null || data.name != null)
            {
                JSONObject js = new JSONObject();
                JSONArray ja = new JSONArray();
                if (data.name != null)
                {
                    js.put("first_name", data.name);
                    ja.put("first_name");
                }
                if (data.lastName != null)
                {
                    js.put("last_name", data.lastName);
                    ja.put("last_name");
                }
                if (data.bio != null)
                {
                    js.put("bio", data.bio);
                    ja.put("bio");
                }
                js.put("updated_parameters", ja);
                send_request(api_url, getbytes(makeData4(js.toString(), "updateProfile")));
            }


            if (data.username != null)
            {
                send_request(api_url, getbytes(makeData4("{\"username\":\"" + data.username + "\"}", "updateUsername")));
            }
        }catch (Exception e){}
    }

    public user_data get_user_info(String userTOKEN) {
        try{
            JSONObject data = new JSONObject(crypto(new JSONObject(send_request(api_url, getbytes(makeData4("{\"user_guid\":\"" + userTOKEN + "\"}", "getUserInfo")))).getString("data_enc"), true));
            user_data userData = new user_data();
            userData.name = data.getJSONObject("user").getString("first_name");
            userData.lastName = data.getJSONObject("user").getString("last_name");
            userData.bio = data.getJSONObject("user").getString("bio");
            userData.username = data.getJSONObject("user").getString("username");
            return userData;
        }catch(Exception e){ return null; }
    }

    public String get_id_token(String id){
        try
        {
            JSONObject s = new JSONObject(crypto(new JSONObject(send_request(api_url, getbytes(makeData4("{\"username\":\"" + id + "\"}", "getObjectByUsername")))).getString("data_enc"), true));
            return s.getJSONObject("user").getString("user_guid");
        }catch(Exception e) { return null; }

    }

    public void delete_message(Long msgID, String chat_token){
        send_request(api_url, getbytes(makeData4("{\"message_ids\":[" + msgID + "],\"object_guid\":\"" + chat_token + "\",\"type\":\"Global\"}", "deleteMessages")));
    }

    public void send_message(String text, String chat_token){
        try{
            JSONObject js = new JSONObject();
            js.put("is_mute", false);
            js.put("object_guid", chat_token);
            js.put("rnd", 100000000 + new Random().nextInt(899999999));
            js.put("text", text);
            String data = makeData4(js.toString(), "sendMessage");
            send_request(api_url, getbytes(data));
        }catch (Exception e){}

    }

    public void send_mention_text(String chat_token, String text, int[][] data, String[] tokens){
        try {
            JSONObject js = new JSONObject();
            js.put("is_mute", false);
            js.put("object_guid", chat_token);
            js.put("rnd", 100000000 + new Random().nextInt(899999999));
            js.put("text", text);

            JSONArray meta_data = new JSONArray();

            for (int i = 0;i< tokens.length ; i++)
            {
                meta_data.put(new JSONObject("{\"from_index\":"+data[i][0]+",\"length\":"+data[i][1]+",\"mention_text_object_guid\":\""+tokens[i]+"\",\"type\":\"MentionText\"}"));
            }
            js.put("metadata", new JSONObject("{ \"meta_data_parts\": "+meta_data+"}"));
            send_request(api_url, getbytes(makeData4(js.toString(), "sendMessage")));
        }catch (Exception e){}
    }

    public void send_replay(String text, long replay_msg_id, String chat_token){
        try {
            JSONObject js = new JSONObject();
            js.put("is_mute", false);
            js.put("object_guid", chat_token);
            js.put("rnd", 100000000 + new Random().nextInt(899999999));
            js.put("text", text);
            js.put("reply_to_message_id", replay_msg_id);
            String data = makeData4(js.toString(), "sendMessage");
            send_request(api_url, getbytes(data));
        }catch (Exception e){}
    }

    public void edit_message(String new_text, Long msg_id, String chat_token){
        try{
            JSONObject js = new JSONObject();
            js.put("object_guid", chat_token);
            js.put("text", new_text);
            js.put("message_id", msg_id);
            String data = makeData4(js.toString(), "editMessage");
            send_request(api_url, getbytes(data));
        }catch (Exception e){}
    }

    public void send_location(String chat_token, double x, double y){
        try{
            JSONObject js = new JSONObject();
            js.put("is_mute", false);
            js.put("object_guid", chat_token);
            js.put("rnd", 100000000 + new Random().nextInt(899999999));
            js.put("location", new JSONObject("{\"latitude\":" + x + ",\"longitude\":" + y + "}"));
            String data = makeData4(js.toString(), "sendMessage");
            send_request(api_url, getbytes(data));
        }catch (Exception e){}
    }

    public MessageData getMessageById(Long msg_id, String chat_token){
        try{
            JSONObject s = new JSONObject(crypto(new JSONObject(send_request(api_url, getbytes(makeData4("{\"message_ids\":[\"" + msg_id + "\"],\"object_guid\":\"" + chat_token + "\"}", "getMessagesByID")))).getString("data_enc").toString(), true)).getJSONArray("messages").getJSONObject(0);
            MessageData data = new MessageData();
            data.type = s.getString("type");
            if (s.has("text"))
            {
                data.text = s.getString("text");
            }
            if (s.has("reply_to_message_id"))
            {
                data.replay = s.getLong("reply_to_message_id");
            }
            data.id = s.getLong("message_id");
            if (data.type != "Event")
            {
                data.sender_token = s.getString("author_object_guid");
            }
            data.json = s;
            return data;
        }catch (Exception e){
            return new MessageData();
        }

    }

    public void Remove_user(String group_token, String user_token) {
        try{
            JSONObject js = new JSONObject();
            js.put("action", "Set");
            js.put("group_guid", group_token);
            js.put("member_guid", user_token);
            String data = makeData5(js.toString(), "banGroupMember");
            send_request(api_url, getbytes(data));
        }catch (Exception e){}
    }

    public void UnRemove_user(String group_token, String user_token){
        try{
            JSONObject js = new JSONObject();
            js.put("action", "Unset");
            js.put("group_guid", group_token);
            js.put("member_guid", user_token);
            String data = makeData5(js.toString(), "banGroupMember");
            send_request(api_url, getbytes(data));
        }catch (Exception e){}
    }

    class admin_access{
        String Set_Member_Access = "SetMemberAccess";
        String Set_Join_Link = "SetJoinLink";
        String Pin_Messages = "PinMessages";
        String Set_Admin = "SetAdmin";
        String Ban_Member = "BanMember";
        String Delete_Messages = "DeleteGlobalAllMessages";
        String Change_Info = "ChangeInfo";
    }

    public void add_admin(String group_token, String user_token, String[] access){
        try{
            JSONArray access_list = new JSONArray();
            for (String s : access)
            {
                access_list.put(s);
            }
            JSONObject js = new JSONObject();
            js.put("access_list", access_list);
            js.put("action", "SetAdmin");
            js.put("group_guid", group_token);
            js.put("member_guid", user_token);
            String data = makeData5(js.toString(), "setGroupAdmin");
            send_request(api_url, getbytes(data));
        }catch (Exception e){}
    }

    public void delete_admin(String group_token, String admin_token) {
        try {
            JSONObject js = new JSONObject();
            js.put("action", "UnsetAdmin");
            js.put("group_guid", group_token);
            js.put("member_guid", admin_token);
            String data = makeData5(js.toString(), "setGroupAdmin");
            send_request(api_url, getbytes(data));
        }catch (Exception e){}
    }

    public void change_group_timer(String group_token, int time) {
        send_request(api_url, getbytes(makeData4("{\"group_guid\":\"" + group_token + "\",\"slow_mode\":" + time + ",\"updated_parameters\":[\"slow_mode\"]}", "editGroupInfo")));
    }

    public void change_group_link(String group_token){
        send_request(api_url, getbytes(makeData4("{\"group_guid\":\"" + group_token + "\"}", "setGroupLink")));
    }

    public String get_group_link(String group_token){
        try{
            JSONObject res = new JSONObject(crypto(new JSONObject(send_request(api_url, getbytes(makeData4("{\"group_guid\":\"" + group_token + "\"}", "getGroupLink")))).getString("data_enc"), true));
            return res.getString("join_link").replace("\\", "");
        }catch(Exception e) { return null; }

    }

    //---------- tools -----------
    public String makeData5(String data, String method) {
        try {
            JSONObject js = new JSONObject();
            js.put("client", new JSONObject("{\"app_name\":\"Main\",\"app_version\":\"2.8.1\",\"lang_code\":\"fa\",\"package\":\"ir.resaneh1.iptv\",\"platform\":\"Android\"}"));
            js.put("input", new JSONObject(data));
            js.put("method", method);
            String data_enc = crypto(js.toString(), false);
            js = new JSONObject();
            js.put("api_version", "5");
            js.put("auth", auth);
            js.put("data_enc", data_enc);
            return js.toString();
        }catch (Exception e){return null;}
    }
    
    public String makeData4(String data, String method) {
        try {
            JSONObject js = new JSONObject();
            js.put("api_version", "4");
            js.put("auth", auth);
            js.put("client", new JSONObject("{\"app_name\":\"Main\",\"app_version\":\"2.8.1\",\"lang_code\":\"fa\",\"package\":\"ir.resaneh1.iptv\",\"platform\":\"Android\"}"));
            js.put("data_enc", crypto(data, false));
            js.put("method", method);
            return js.toString();
        }catch (Exception e){
            return null;
        }

    }

    public String send_request(String url, byte[] data){
        try{
            HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
            c.setRequestMethod("POST");
            c.setRequestProperty("Accept", "application/json");
            c.addRequestProperty("Content-Type", "application/json");
            c.setDoOutput(true);
            c.getOutputStream().write(data);
            InputStream s = c.getInputStream();
            return new BufferedReader(new InputStreamReader(s)).readLine();
        }catch(Exception e) {
            return null;
        }
    }

    private byte[] getbytes(String d){return d.getBytes();}

    private String crypto(String data, Boolean b){
        if (b){
            data = data.replace("\\n", "\n");
            try{
                Cipher instance = Cipher.getInstance("AES/CBC/PKCS5PADDING");
                instance.init(2, key_aes, iv_aes);
                String sb =  new String(instance.doFinal(android.util.Base64.decode(data.getBytes(),0)));
                return sb;
            }catch(Exception e){return null;} 
        }else{
            try{
                Cipher instance = Cipher.getInstance("AES/CBC/PKCS5PADDING");
                instance.init(Cipher.ENCRYPT_MODE, key_aes, iv_aes);
                byte[] doFinal = instance.doFinal(data.getBytes());
                String sb = Base64.encodeToString(doFinal,0);
                return sb;
            }catch(Exception e){return null;}
        }
    }

}