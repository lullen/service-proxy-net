using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Proxy;
using Proxy.NewProxy;
using Test.Interfaces;

namespace Test
{
    public class ImplService : ServiceOne, ServiceTwo
    {
        public Task<Response<MethodClass>> MethodOne(MethodClass request)
        {
            if (request.Text.Contains("next"))
            {
                return Task.FromResult(new Response<MethodClass>(new Error(ErrorCode.InvalidInput, "Next is forbidden")));
            }

            Console.WriteLine("Method one called with request: " + request.Text);
            return Task.FromResult(new Response<MethodClass>(new MethodClass { Text = request.Text }));
        }

        public Task<Response<MethodClass3>> MethodThree(MethodClass3 request)
        {
            if (request.Text.Contains("next"))
            {
                return Task.FromResult(new Response<MethodClass3>(new Error(ErrorCode.InvalidInput, "Next is forbidden")));
            }

            Console.WriteLine("Method three called with request: " + request.Text);
            return Task.FromResult(new Response<MethodClass3>(new MethodClass3 { Text = request.Text }));
        }

        public Task<Response<MethodClass2>> MethodTwo(MethodClass2 request)
        {
            if (request.Text.Contains("next"))
            {
                return Task.FromResult(new Response<MethodClass2>(new Error(ErrorCode.InvalidInput, "Next is forbidden")));
            }

            Console.WriteLine("Method two called with request: " + request.Text);
            return Task.FromResult(new Response<MethodClass2>(new MethodClass2 { Text = request.Text }));
        }
    }

    public class MethodClass : IMessage
    {
        public string Text { get; set; }

        public MessageDescriptor Descriptor => throw new NotImplementedException();

        public int CalculateSize()
        {
            throw new NotImplementedException();
        }

        public void MergeFrom(CodedInputStream input)
        {
            throw new NotImplementedException();
        }

        public void WriteTo(CodedOutputStream output)
        {
            throw new NotImplementedException();
        }
    }
    
    public class MethodClass2 : IMessage
    {
        public string Text { get; set; }

        public MessageDescriptor Descriptor => throw new NotImplementedException();

        public int CalculateSize()
        {
            throw new NotImplementedException();
        }

        public void MergeFrom(CodedInputStream input)
        {
            throw new NotImplementedException();
        }

        public void WriteTo(CodedOutputStream output)
        {
            throw new NotImplementedException();
        }
    }
    
    public class MethodClass3 : IMessage
    {
        public string Text { get; set; }

        public MessageDescriptor Descriptor => throw new NotImplementedException();

        public int CalculateSize()
        {
            throw new NotImplementedException();
        }

        public void MergeFrom(CodedInputStream input)
        {
            throw new NotImplementedException();
        }

        public void WriteTo(CodedOutputStream output)
        {
            throw new NotImplementedException();
        }
    }

    public class Testing
    {
        private readonly ServiceProxy sp;

        public Testing(ServiceProxy sp)
        {
            this.sp = sp;
        }
        public async Task Run()
        {
            var ress = await sp.Create<ServiceOne>().MethodOne(new MethodClass { Text = "Hi there!" })
                .Next((res) => sp.Create<ServiceTwo>().MethodTwo(new MethodClass2 { Text = res.Text + " Hello!" }))
                .Next((res) => sp.Create<ServiceTwo>().MethodThree(new MethodClass3 { Text = res.Text + " THERE!" }))
                .OnError((error) =>
                {
                    Console.WriteLine("ERROR!!! " + error.Description);
                    return Task.FromResult(new Response<MethodClass3>(error));
                });

            if (ress.HasError)
            {
                Console.WriteLine("Returned error " + ress.Error.Description);
            }
            else
            {
                Console.WriteLine("Returned " + ress.Result.Text);
            }

        }
    }
}