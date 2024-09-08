using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

public class RpcRequest
{
    public string RequestId { get; set; } // Add a RequestId to identify the request
    public string Command { get; set; }
    public object Parameters { get; set; }
}

public class RpcResponse
{
    public string RequestId { get; set; } // Add RequestId to match with the request
    public string Result { get; set; }
    public string Error { get; set; }
    public object Parameters { get; set; }
}