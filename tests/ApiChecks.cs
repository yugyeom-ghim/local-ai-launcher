using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SILocalAI;
public static class ApiChecks
{
 static int passed,failed;
 static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Yes(bool b){if(!b)throw new Exception("assertion failed");}
 static void Throws(Action a){try{a();}catch{return;}throw new Exception("expected failure");}
 public static int Main(string[] args)
 {
  string state=Path.Combine(Path.GetTempPath(),"si-api-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(state);
  string key="SYNTHETIC-TEST-CREDENTIAL-0123456789012345";
  try {
   Test("all control endpoint requests carry bearer auth",delegate {
    foreach(string path in new[]{"/health","/v1/models","/models","/models/load","/v1/chat/completions"})
     using(var r=LocalHttp.Request(2345,path,path.EndsWith("load")||path.EndsWith("completions")?(object)new{model="m"}:null,key)){
      Yes(r.RequestUri.Host=="127.0.0.1"&&r.RequestUri.Port==2345);Yes(r.Headers.Authorization.Scheme=="Bearer");Yes(r.Headers.Authorization.Parameter==key);
      Yes(!r.RequestUri.ToString().Contains(key)); if(r.Content!=null)Yes(r.Content.Headers.ContentType.MediaType=="application/json");
     }
   });
   Test("keyless request does not send auth",delegate {using(var r=LocalHttp.Request(1234,"/v1/models",null,""))Yes(r.Headers.Authorization==null);});
   Test("external and malformed API paths rejected",delegate {
    foreach(string path in new[]{"https://example.com/","//example.com/","/\\example.com/","/v1/models\n",""})Throws(delegate {using(var r=LocalHttp.Request(1234,path,null,key)){};});
   });
   Test("credentials removed from process environment",delegate {
    string old=Environment.GetEnvironmentVariable("LLAMA_API_KEY"),old2=Environment.GetEnvironmentVariable("LLAMA_ARG_HOST");
    try {Environment.SetEnvironmentVariable("LLAMA_API_KEY",key);Environment.SetEnvironmentVariable("LLAMA_ARG_HOST","0.0.0.0");
     var i=OwnedProcess.Info(Path.Combine(state,"llama-server.exe"),new[]{"--help"},state,state,state);
     Yes(!i.EnvironmentVariables.ContainsKey("LLAMA_API_KEY"));Yes(!i.EnvironmentVariables.ContainsKey("LLAMA_ARG_HOST"));Yes(i.EnvironmentVariables["LLAMA_ARG_OFFLINE"]=="1");Yes(!i.Arguments.Contains(key));
    } finally{Environment.SetEnvironmentVariable("LLAMA_API_KEY",old);Environment.SetEnvironmentVariable("LLAMA_ARG_HOST",old2);}
   });
   Test("log file and event both redact local API key",delegate {
    string logPath=Path.Combine(state,"log.txt"),captured="";var log=new AppLog(logPath);log.HideSecret(key);log.Line+=delegate(string line){captured=line;};log.Write("key="+key);
    Yes(!captured.Contains(key));Yes(!File.ReadAllText(logPath).Contains(key));Yes(log.Clean(key).Contains("hidden"));
   });
   Test("connection status never serializes bearer token",delegate {
    string text=Json.Write(new ServerStatus{ApiKeyRequired=true,Thinking="OFF",Context=4096});Yes(!text.Contains("\"ApiKey\":"));Yes(!text.Contains(key));
   });
   Test("stopped server cannot export stale connection",delegate {
    using(var server=new ServerController(state,state,new AppLog(Path.Combine(state,"server.log"))))Throws(delegate{server.Connection();});
   });
   Test("exported context follows running server with bounded output",delegate {
    foreach(int context in new[]{512,4096,8192,16384,32768,65536,131072,262144,70000,1010000,Int32.MaxValue}) {
     var c=new ClientConnection{BaseUrl="http://127.0.0.1:1234/v1",ModelId="test",ModelName="Test",Context=context};
     var doc=Json.Read<Dictionary<string,object>>(ClientConfig.OpenCode(c));
     var providers=(Dictionary<string,object>)doc["provider"];var provider=(Dictionary<string,object>)providers[ClientConfig.ProviderId];
     var models=(Dictionary<string,object>)provider["models"];var model=(Dictionary<string,object>)models["test"];var limits=(Dictionary<string,object>)model["limit"];
     Yes(Convert.ToInt32(limits["context"])==context);int output=Convert.ToInt32(limits["output"]);Yes(output>0 && output<context);
     Yes(!doc.ContainsKey("mcp") && !doc.ContainsKey("permission") && !doc.ContainsKey("agent"));
    }
   });
   foreach(bool required in new[]{false,true}) {
    var c=new ClientConnection{BaseUrl="http://127.0.0.1:1234/v1",ModelId="Qwen3.5-4B-Q4_K_M-12345678",ModelName="Qwen3.5-4B-Q4_K_M",Context=16384,ApiKeyRequired=required,ApiKey=key};
    File.WriteAllText(Path.Combine(args[0],required?"opencode-auth-test.json":"opencode-noauth-test.json"),ClientConfig.OpenCode(c));
   }
  }finally{Directory.Delete(state,true);}
  Console.WriteLine("API logic checks: "+passed+" passed, "+failed+" failed; no live server/network inference test");return failed==0?0:1;
 }
}
