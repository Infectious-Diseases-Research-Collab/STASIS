# STASIS Project Structure and Infrastructure

This document provides a detailed explanation of the folder structure and the key files in the STASIS project. Understanding this structure is essential for working with this .NET Core Razor Pages application.

## High-Level Overview

This project is a .NET Core Razor Pages application. Razor Pages is a page-focused framework for building web UIs in ASP.NET Core. It follows a convention-based approach, where the file system structure directly maps to the URL structure of the application.

The project is organized into several key directories:

-   **`/` (Root):** Contains project-level files, configuration, and the main application entry point.
-   **`/Pages`:** The core of the Razor Pages application, containing the web pages and their backing logic.
-   **`/wwwroot`:** Contains all the static assets for the application, such as CSS, JavaScript, and images.
-   **`/STASIS`:** A directory containing the core application logic, including data models, services, and database-related code. This is a good practice for separating the web front-end from the back-end business logic.
-   **`/Properties`:** Contains project configuration, such as launch settings.

---

## Detailed File and Folder Descriptions

### Root Directory (`/`)

-   **`STASIS.sln`:** The Visual Studio Solution file. It's a container for one or more projects. You can open this file in Visual Studio to load the entire solution.
-   **`STASIS.csproj`:** The C# project file for the STASIS application. It contains project-specific settings, such as the target framework, dependencies (NuGet packages), and which files to include in the build.
-   **`Program.cs`:** The entry point of the application. This file sets up the web server (Kestrel), configures the ASP.NET Core pipeline, and registers services for dependency injection. This is where the application starts running.
-   **`appsettings.json` and `appsettings.Development.json`:** Configuration files. These JSON files store configuration data for the application, such as database connection strings, logging settings, and other application-level settings. `appsettings.Development.json` overrides the settings in `appsettings.json` when the application is running in the "Development" environment.
-   **`.gitignore`:** A file that tells Git which files and folders to ignore from source control (e.g., temporary files, build outputs).

### `/Pages` Directory

This is where the user interface of the application lives. Razor Pages use a file-based routing system. For example, a file at `/Pages/Samples/Search.cshtml` will be accessible at the URL `/Samples/Search`.

-   **`_ViewStart.cshtml`:** This file contains code that is executed at the start of each Razor Page's rendering. It's commonly used to set the default layout for all pages.
-   **`_ViewImports.cshtml`:** This file is used to provide namespaces to all other Razor Pages, so you don't have to add `@using` statements for common namespaces in every file.
-   **`_Layout.cshtml`:** The main layout file for the application. It defines the common HTML structure that wraps all other pages (e.g., the header, navigation, footer). Content from other pages is rendered inside the `@RenderBody()` section.
-   **`Error.cshtml`:** The page that is displayed when an unhandled error occurs in the application.

#### Razor Page Files

Each page in the application is typically represented by two files:

-   **`.cshtml` (Razor View):** This file contains the HTML markup and embedded C# code (using Razor syntax) that defines the user interface of the page.
-   **`.cshtml.cs` (Page Model):** This file contains the C# class that acts as the "code-behind" for the Razor Page. The Page Model handles the logic for the page, such as handling user input, fetching data from a database, and setting properties that can be accessed by the Razor View.

**Example:**
-   `Pages/Samples.cshtml` (the view)
-   `Pages/Samples.cshtml.cs` (the Page Model)

Subdirectories within `/Pages` create a hierarchical URL structure. For example, files in `/Pages/Samples/` will have URLs starting with `/Samples/`.

### `/wwwroot` Directory

This directory contains all the static assets that are served directly to the client's browser. The contents of this folder are publicly accessible.

-   **`/css`:** Contains the application's stylesheets (CSS files). `site.css` is the main stylesheet for the application.
-   **`/js`:** Contains JavaScript files. `site.js` is for custom JavaScript code.
-   **`/lib`:** This folder is conventionally used for third-party client-side libraries, such as Bootstrap and jQuery.
-   **`/images`:** Contains image files used in the application.
-   **`favicon.ico`:** The icon for the website that is displayed in the browser's tab.

### `/STASIS` Directory

This directory is a custom organization for this project, and it's a good practice to separate the core application logic from the UI (the `Pages` directory).

-   **`/Data`:** Contains the database context class.
    -   **`StasisDbContext.cs`:** This file defines the Entity Framework Core database context. This class represents a session with the database and allows you to query and save data. It defines the tables (as `DbSet` properties) that are part of your database model.
-   **`/Models`:** Contains the C# classes that represent the data entities of the application. These are often called "POCOs" (Plain Old CLR Objects). For example, `Specimen.cs`, `Box.cs`, `Freezer.cs`, etc. These classes map to the tables in the database.
-   **`/Services`:** Contains the application's business logic. Services are used to encapsulate logic for specific domains (e.g., `SampleService`, `StorageService`). They are injected into the Page Models to provide the necessary functionality. This promotes separation of concerns and makes the code more testable and maintainable.
    -   **Interfaces (`.cs`):** Defines the contract for the service (e.g., `ISampleService.cs`).
    -   **Implementations (`.cs`):** Contains the actual implementation of the service (e.g., `SampleService.cs`).
-   **`setup.sql`:** A SQL script that can be used to set up the initial database schema.

### `/Properties` Directory

-   **`launchSettings.json`:** This file contains settings for launching the application locally from Visual Studio or the command line. It defines profiles for different launch configurations (e.g., running with IIS Express or as a standalone console application). It's where you can configure environment variables, such as `ASPNETCORE_ENVIRONMENT`.

### Other Directories

-   **`.git`:** A hidden folder that contains all the information about your local Git repository.
-   **`.vs`:** A hidden folder that Visual Studio uses to store its own information about the solution, such as open files, window layouts, etc. It's safe to delete this folder; Visual Studio will regenerate it.
-   **`bin` and `obj`:** These folders contain the compiled output of the project (the `.dll` and `.exe` files) and intermediate files used during the build process. These are generated automatically and should not be included in source control.

---

This structure provides a clean and organized way to build a web application. The separation of concerns between the UI (`Pages`), the business logic (`Services`), the data models (`Models`), and the data access (`Data`) is a key principle of modern software development that makes the application easier to develop, test, and maintain.
