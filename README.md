# Crank
Crank is a load testing tool for SignalR.

## How to use it
1. [Download](https://github.com/SignalR/crank/downloads) a copy of crank
2. Point crank at a [PersisentConnection](https://github.com/SignalR/SignalR/wiki/PersistentConnection) end point

**Example**

    crank http://mysite/echo 5000
    
You can optionally change the batch size and timeout between batches.

## Getting the most out of crank
When running `crank` you need to increase the MaxUserPort setting on the machine. For more 
information on how to do this, read this article:

http://support.microsoft.com/kb/196271
