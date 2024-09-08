using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

public class RpcRequest
{
    public string Command { get; set; }
    public Object Parameters { get; set; }
}

public class RpcResponse
{
    public string Result { get; set; }  // The result of the RPC call, such as a calculated value or a success message
    public string Error { get; set; }   // An error message in case something went wrong

    public Object Parameters { get; set; }  // Additional parameters that can be sent along with the response
}