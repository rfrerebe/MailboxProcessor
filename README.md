# MailboxProcessor
Single Threaded MailboxProcessor written in C# using System.Threading.Channels


This is a simple actor system for asynchronous message processing.
I needed it to parse XML files and convert a single file into a bunch of CSV files
for loading them using Oracle SQL Loader.

Each agent is capable of writing into a single file in a thread safe manner.
Each agent can also send messages to other agents for further message processing.

Agents are lightweight so you can instantiate a lot of them without consuming a lot of OS resources.
