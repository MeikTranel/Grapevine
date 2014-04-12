﻿using System.Net;
using Grapevine;

namespace SampleServer
{
    class RestServer : HttpResponder
    {
        [Responder(Method = HttpMethod.GET, PathInfo = @"^/foo/\d+$")]
        public void HandleFoo(HttpListenerContext context)
        {
            this.SendResponse(context, "Foo is a success!");
        }

        [Responder(Method = HttpMethod.GET, PathInfo = @"^/foo/\D+$")]
        public void HandleMoeFoo(HttpListenerContext context)
        {
            context.Response.StatusDescription = "I'm a teapot";
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 418;

            this.SendTextResponse(context, "Make your own coffee, foo!");
        }

        [Responder(Method = HttpMethod.POST, PathInfo = @"/.?")]
        public void HandleAllPosts(HttpListenerContext context)
        {
            this.SendTextResponse(context, "All Post Go to Heaven");
        }

        [Responder(Method = HttpMethod.DELETE, PathInfo = @"^/shutdown$")]
        public void RemoteShutDown(HttpListenerContext context)
        {
            this.SendResponse(context, "Shutting down, Mr. Bond...");
            this._listening = false;
        }
    }
}