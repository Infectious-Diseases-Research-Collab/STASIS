# STASIS

This project is the Specimen Tracking And Storage Information System (STASIS).

## Local Setup Instructions

To get STASIS up and running on your local machine, follow these steps:

1.  **Database Setup:**
    *   Ensure you have a SQL Server instance running. The application expects a database named `STASIS`.
    *   Execute the `STASIS/setup.sql` script against your SQL Server instance to create the necessary database schema and seed initial data.

2.  **Configure Database Connection:**
    *   Open `appsettings.json` in the project root.
    *   Update the `DefaultConnection` connection string to point to your local SQL Server instance. Replace `YOUR_SQL_SERVER_NAME` with the actual name or IP address of your SQL Server.

    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=YOUR_SQL_SERVER_NAME;Database=STASIS;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
      },
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "AllowedHosts": "*"
    }
    ```

3.  **Run the Application:**
    *   Build and run the project from Visual Studio or using the .NET CLI:
        ```bash
        dotnet run
        ```
    *   The application will typically launch in your default web browser.
