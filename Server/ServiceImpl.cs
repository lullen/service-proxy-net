using System;
using System.Threading.Tasks;
using Proxy.Client;
using Proxy.Models;
using Proxy.Server;
using Server.Interfaces;

namespace Server
{

    [ExposedService]
    public class ServiceImpl : ServiceOne, ServiceTwo, IService
    {
        private readonly CurrentUser currentUser;

        public ServiceImpl(CurrentUser currentUser)
        {
            this.currentUser = currentUser;
        }
        public Task<Response<MethodResponseOne>> MethodOne(MethodRequestOne request)
        {
            if (request.Text.Contains("next"))
            {
                return Task.FromResult(new Response<MethodResponseOne>(new Error(ErrorCode.InvalidInput, "Next is forbidden")));
            }

            Console.WriteLine("Method one called with request: " + request.Text + ". " + currentUser.Token);
            return Task.FromResult(new Response<MethodResponseOne>(new MethodResponseOne { Text = request.Text }));
        }

        public Task<Response<MethodResponseTwo>> MethodTwo(MethodRequestTwo request)
        {
            if (request.Text.Contains("next"))
            {
                return Task.FromResult(new Response<MethodResponseTwo>(new Error(ErrorCode.InvalidInput, "Next is forbidden")));
            }

            Console.WriteLine("Method two called with request: " + request.Text);
            return Task.FromResult(new Response<MethodResponseTwo>(new MethodResponseTwo { Text = request.Text }));
        }

        public Task<Response<MethodResponseThree>> MethodThree(MethodRequestThree request)
        {
            if (request.Text.Contains("next"))
            {
                return Task.FromResult(new Response<MethodResponseThree>(new Error(ErrorCode.InvalidInput, "Next is forbidden")));
            }

            Console.WriteLine("Method three called with request: " + request.Text);
            return Task.FromResult(new Response<MethodResponseThree>(new MethodResponseThree { Text = request.Text }));
        }

        public async Task<Response<MethodResponseOne>> UploadFile(FileTestRequest request)
        {
            Console.WriteLine($"Saving file {request.Name} with id {request.Id}");
            using var file = System.IO.File.Create($"{request.Name}.txt");
            await request.File.CopyToAsync(file);
            file.Flush();
            file.Close();
            return new Response<MethodResponseOne>(new MethodResponseOne { Text = "File saved" });
        }

    }
}
