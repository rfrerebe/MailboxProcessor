# MailboxProcessor
Single Threaded MailboxProcessor (Mini Agent) written in C# using System.Threading.Channels


This is a simple agent for asynchronous message processing with preserving the state.
I needed it to parse XML files and convert a single file into a bunch of CSV files
for loading them using Oracle SQL Loader.

Each agent is capable of writing into a single file in a thread safe manner.
Each agent can also send messages to other agents for further message processing.

With ScanHandler messages can be expected in a separate handler, and be sent to another agent for processing.

Agents are lightweight so you can instantiate a lot of them without consuming a lot of OS resources.

Thge Test application creates agents to write into separate files - each agents handles its own file
and writes to it in a thread safe manner.
