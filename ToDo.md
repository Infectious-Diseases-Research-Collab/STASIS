## To Do
- [ ] create instructions for new developers who clone the repo to get up and running
- [ ] on the view samples page - show more than 25 records at a time


Storing passwords in plain text in appsettings.json is a security risk, especially if that file gets committed to source control (Git).

  For your Windows Server production environment, the industry standard best practice is to use Environment Variables.

  How it works
  ASP.NET Core is built to look for configuration values in multiple places. It follows this order:
   1. appsettings.json (Lowest priority)
   2. appsettings.Production.json
   3. Environment Variables (Highest priority - overrides everything else)

  The Solution: Use Environment Variables
  Instead of putting the real password in the file, you keep the file like this:
   1 "EmailSettings": {
   2     "Password": ""  // Leave empty or put a dummy value
   3 }

  Then, on your Windows Server, you create an Environment Variable with this specific name (note the double underscore __ which represents the nested JSON structure):

   * Name: EmailSettings__Password
   * Value: YourActualSecurePassword

  When the application starts, it will see the empty value in the JSON file, look for an environment variable, find it, and automatically use the real password from the server's memory.

  How to set this on Windows Server
   1. Open System Properties > Environment Variables.
   2. Under System Variables, click New.
   3. Enter the Name (EmailSettings__Password) and Value.
   4. Restart the server (or just the application pool) for it to take effect.

  For Development (Right Now)
  For your local machine, we can use a tool called "User Secrets". This saves the password in a file hidden in your personal Windows User folder (%APPDATA%), completely outside the project directory.

  Would you like me to move your current SMTP settings into "User Secrets" so we can clean up your `appsettings.json` file?
