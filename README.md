# MailboxProcessor
Single Threaded MailboxProcessor (Mini Agent) written in C# using System.Threading.Channels.

Currently i could not find a good robust agent implementation for C#, so i wrote my own.
There exists one for F#, but i don't want to learn this language. 


This is a simple agent for asynchronous message processing with preserving the state.
I needed it to parse XML files and convert a single file into a bunch of CSV files
for loading them using Oracle SQL Loader.

Each agent is capable of writing into a single file in a thread safe manner.
Each agent can also send messages to other agents for further message processing.

The Test application creates agents to write into separate files - each agents handles its own file
and writes to it in a thread safe manner.

With MessageScanHandler the messages can be inspected in a separate handler, and be sent to another agent for processing,
or they even can be replaced with other type of messages.

Agents are lightweight so you can instantiate a lot of them without consuming a lot of OS resources.


Now, i'm planning to use it for wrapping a Device manager on the server, so it could be single threaded (because the device needs only
sequential access, and using an agent is better than to use locking in this case)
