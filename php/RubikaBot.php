<?php

function MakeKey(string $auth){
    $b = "";
    $b .= substr($auth, 16, 8);
    $b .= substr($auth, 0, 8);
    $b .= substr($auth, 24);
    $b .= substr($auth, 8, 8);
    for ($i = 0; $i < strlen($b); $i++) {
        if ($b[$i] >= '0' && $b[$i] <= '9') {
            $b[$i] = chr((((ord($b[$i]) - 48) + 5) % 10) + 48);
        }
        if ($b[$i] >= 'a' && $b[$i] <= 'z') {
            $b[$i] = chr((((ord($b[$i]) - 97) + 9) % 26) + 97);
        }
    }
    return $b;
}

function hasKey($json,$key){
    if(!empty($json[$key]) && $json[$key] != ''){
        return true;
    }else{
        return false;
    }
}

class user_data{
    public $user_name;
    public $user_guid;
    public $name;
    public $last_name;
    public $bio;
};

class message_data{
    public $id;
    public $sender_token;
    public $text;
    public $replay;
    public $type;
    public $json;
};

class Rubika{
    public $auth;
    public $api_url = "https://messengerg2c19.iranlms.ir";
    public $aes_key;
    public $aes_iv;

    public function __construct(string $auth){
        $this->aes_iv = str_repeat("\x00", 16);
        $this->aes_key = MakeKey($auth);
        $this->auth = $auth;
    }

    public function decrypt(string $data){
        $dec = openssl_decrypt(base64_decode($data), "AES-256-CBC", $this-> aes_key, OPENSSL_RAW_DATA, $this-> aes_iv);
        return $dec;
    }

    public function encrypt(string $data){
        $en = base64_encode(openssl_encrypt($data, "AES-256-CBC", $this->aes_key, OPENSSL_RAW_DATA, $this->aes_iv));
        return $en;
    }

    public function pars_response(string $response){
        $response = json_decode($response,true);
        $data_dec = $this->decrypt($response["data_enc"]);
        $json = json_decode($data_dec,true);
        return $json;
    }

    public function make_data($data,string $method){
        $data_enc = $this->encrypt(json_encode($data));
        $s = ["api_version"=>"4","auth"=>$this->auth,"client"=>["app_name"=>"Main","app_version"=>"2.8.1","lang_code"=>"fa","package"=>"ir.resaneh1.iptv","platform"=>"Android"],"data_enc"=>$data_enc,"method"=>$method];
        return json_encode($s);
    }

    public function sender($data,string $method){
        $body = $this->make_data($data,$method);
        $response = send_post($this->api_url,$body);
        return $this->pars_response($response);
    }

    public function send_message($text,$chat_token,$replay=null){
        $js = ["is_mute"=>False,"object_guid"=>$chat_token,"rnd"=>random_int(111111111,999999999),"text"=>$text];
        if ($replay !== null){
            $js["reply_to_message_id"] = $replay;
        }
        return $this->sender($js,"sendMessage");
    }

    public function search_in_member($text,$channel_guid){
        $j1 = ["channel_guid"=>$channel_guid,"search_text"=>$text];
        $res = $this->sender($j1,"getChannelAllMembers");
        try{
            return $res["in_chat_members"];
        }catch (Exception $e){return [];}
    }

    public function get_user_info($token){
        $data = new user_data();
        $res = $this->sender(["user_guid"=>$token],"getUserInfo");
        if(hasKey($res["user"],"first_name")){
            $data->name = $res["user"]["first_name"];
        }
        
        if(hasKey($res["user"],"last_name")){
            $data->last_name = $res["user"]["last_name"];
        }
        
        if(hasKey($res["user"],"username")){
            $data->user_name = $res["user"]["username"];
        }

        if(hasKey($res["user"],"bio")){
            $data->bio = $res["user"]["bio"];
        }

        return $data;
    }

    public function check_join($member_guid,$channel_guid){
        $is_join = false;
        $info = $this->get_user_info($member_guid);
        if (hasKey($info,"user_name")){
            $username = $info["user_name"];
            $res = $this->search_in_member($username,$channel_guid);
            foreach ($res as $i){
                if ($i["member_guid"] == $member_guid){
                    return true;
                }
            }
        }
        if (hasKey($info,"name")){
            $name = $info["name"];
            $res = $this->search_in_member($name,$channel_guid);
            foreach ($res as $i){
                if ($i["member_guid"] == $member_guid){
                    return true;
                }
            }
        }
        if (hasKey($info,"last_name")){
            $last_name = $info["last_name"];
            $res = $this->search_in_member($last_name,$channel_guid);
            foreach ($res as $i){
                if ($i["member_guid"] == $member_guid){
                    return true;
                }
            }
        }
        return false;
    }

    public function update_profile(user_data $data){
        $js = [];
        $ja = [];
        if ($data->name !== null)
        {
            $js["first_name"] = $data->name;
            array_push($ja , "first_name");
        }
        if ($data->last_name !== null)
        {
            $js["last_name"] = $data->last_Name;
            array_push($ja,"last_name");
        }
        if ($data->bio !== null)
        {
            $js["bio"] = $data->bio;
            array_push($ja,"bio");
        }
        if ($js !== []){
            $js["updated_parameters"] = $ja;
            $this->sender($js,"updateProfile");
        }

        if ($data->user_name != null)
        {
            $this->sender(["username" => $data->user_name],"updateUsername");
        }
    }

    public function delete_message($chat_token,$message_id){
        $j1 = ["message_ids"=>[$message_id],"object_guid" => $chat_token ,"type" => "Global"];
        $this->sender($j1,"deleteMessages");
    }

    public function send_mention_text($chat_token, $text, $data, $tokens){
        $js = ["is_mute"=>false,"object_guid"=>$chat_token,"rnd"=>random_int(100000000, 999999999),"text"=>$text];
        $meta_data = [];

        for ($i = 0;$i< count($tokens); $i++)
        {
            array_push($meta_data,["from_index"=>$data[$i][0],"length"=>$data[$i][1],"mention_text_object_guid"=>$tokens[$i],"type"=>"MentionText"]);

        }

        $js["metadata"] = ["meta_data_parts"=> $meta_data];
        $this->sender($js,"sendMessage");
    }

    public function edit_message($chat_token,$new_text,$message_id){
        $js = ["object_guid"=>$chat_token,"text"=>$new_text,"message_id"=> (string) $message_id];
        $this->sender($js,"editMessage");
    }

    public function get_message_by_id($chat_token,$message_id){
        $js = ["message_ids"=>[(string) $message_id],"object_guid"=>$chat_token];
        $res = $this->sender($js,"getMessagesByID")["messages"][0];
        $data = new message_data();
        if (hasKey($res,"text")){
            $data->text = $res["text"];
        }
        if(hasKey($res,"reply_to_message_id")){
            $data->replay = $res["reply_to_message_id"];
        }
        $data->id = $res["message_id"];
        $data->json = $res;
        $data->sender_token = $res["author_object_guid"];
        $data->type = $res["type"];
        return $data;
    }

    public function remove_user($chat_token,$member_guid){
        $js = ["action"=>"Set","group_guid"=>$chat_token,"member_guid"=>$member_guid];
        $this->sender($js,"banGroupMember");
    }

    public function UnRemove_user($chat_token,$member_guid){
        $js = ["action"=>"Unset","group_guid"=>$chat_token,"member_guid"=>$member_guid];
        $this->sender($js,"banGroupMember");
    }

    public function get_Token_By_Id($id){
        if (strpos($id,"@") !== false){
            $id = str_replace("@","",$id);
        }
        $js = ["username"=> $id];
        $res = $this->sender($js,"getObjectByUsername");
        return $res["user"]["user_guid"];
    }
} 
?>
