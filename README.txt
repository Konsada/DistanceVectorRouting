In the directory containing this text document please find the make files <INSERT HERE>. Each of these corresponds to one of the test cases (1,2,cti) based on name. For each router that you wish to create do the following:
1. Open a command prompt
2. Use the cd command to enter the directory that this file resides in
3. Enter the command router [-p] testDirectory routerName where [-p] represents the option to input -p for poise and reverse functionality, testDirectory is the name of the directory containing the test cases you wish to run, and routerName is the name of the router you wis to create

After creating each of the desired routers, the program should run normally. You may, if you desire, make requests to print routing tables or request changes to links between routers. For each of these options please create a seperate command prompt to use first. 
To request printing of a routing table run the command: python printtables testdir r, where testdir is the test case directory name and r is the router name you wish to do printing of. Alternatively you may run the command: python printtables testdir if you wish to request printing of routing tables for ALL routers.
To change the link between routers enter the command: python changelink testdir r1 r2 c, where testdir is the name of the test case directory, r1 and r2 are router names being affected, and c is the new cost you are changing the link to. 

When you feel the program has run for long enough, feel free to stop the command prompts.