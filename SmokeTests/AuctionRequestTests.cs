using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BuffAssistant.Services;

internal static class AuctionRequestTests
{
    public static void Run(Action<bool,string> check)
    {
        check(AuctionService.NormalizeQuery("  희미한\u200b　에너지\t조각 ") == "희미한 에너지 조각", "Search normalizes copied invisible characters and nonstandard spaces");
        var names = new ItemNameCatalog();
        check(names.ResolveName("희미한에너지조각") == "희미한 에너지 조각", "Space-free item name resolves to the official catalog name");
        check(StartupService.BuildCommand(@"C:\Games\Black Card\helper.exe",true) == "\"C:\\Games\\Black Card\\helper.exe\" --background", "Startup registration quotes paths with spaces and supports background launch");
        using(var handler=new ReplyHandler("OPENAPI00004"))
        using(var client=new HttpClient(handler))
        using(var service=new AuctionService(client))
        {
            var result=service.SearchAsync("검",false,null,CancellationToken.None).GetAwaiter().GetResult();
            check(handler.Calls==2 && handler.LastPath.EndsWith("/keyword-search") && result.Items.Count==1,"Invalid exact-name conditions retry the keyword endpoint successfully");
        }
        using(var handler=new ReplyHandler("OPENAPI00005"))
        using(var client=new HttpClient(handler))
        using(var service=new AuctionService(client))
        {
            try { service.SearchAsync("검",false,null,CancellationToken.None).GetAwaiter().GetResult(); check(false,"Invalid credential must be reported"); }
            catch(AuctionApiException e) { check(handler.Calls==1 && e.Code=="OPENAPI00005" && e.Message.Contains("내장 API 키"),"Credential rejection is distinguished from search errors and never retried as a keyword"); }
        }
    }
    private sealed class ReplyHandler(string code):HttpMessageHandler
    {
        public int Calls; public string LastPath="";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++; LastPath=request.RequestUri.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(Calls==1?HttpStatusCode.BadRequest:HttpStatusCode.OK) {
                Content=new StringContent(Calls==1 ? "{\"error\":{\"name\":\""+code+"\"}}" : "{\"auction_item\":[{\"item_name\":\"검\"}]}" ) });
        }
    }
}
