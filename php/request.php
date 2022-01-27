<?php

$ngix_error = "The page you are looking for is temporarily unavailable";

function send_post($url,$data){
    while (true){
        $ch = curl_init($url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
        curl_setopt($ch, CURLOPT_POSTFIELDS, $data);
        $response = curl_exec($ch);
        global $ngix_error;
        if ($response != null & strpos($response,$ngix_error) == false){
            break;
        }
    }

    return $response;
}

function send_get($url){
    return file_get_contents($url);
}
?>
