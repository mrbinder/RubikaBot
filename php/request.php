<?php

$ngix_error = "The page you are looking for is temporarily unavailable";

function send_post($url,$data){
    while (true){
        $ch = curl_init($url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
        $headers = [
            'content-type: application/json; charset=UTF-8',
        ];
        curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
        curl_setopt($ch, CURLOPT_POSTFIELDS, $data);
        $response = curl_exec($ch);
        global $ngix_error_bypass;
        if ($response != null & strpos($response,$ngix_error_bypass) == false){
            break;
        }
    }

    return $response;
}

function send_get($url){
    return file_get_contents($url);
}
?>
