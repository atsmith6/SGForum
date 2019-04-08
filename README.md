# SGForum

## Pre-Alpha Notice

SGForum is still in very early pre-alpha stages of development.

## Overview

SGForum is a base forum template that can be used to remove the intial work involved in creating a custom forum for any community.  It is divided into a JSON API layer and an HTTPS Web layer to make access over the web or by mobile or desktop apps seamless and easy.  All communication at the API layer is performed over HTTPS using JSON.

## Platform Notes

Be aware that at the current time this is built in Visual Studio Code on Linux using MySQL 8 as a database.  Future updates will add Mongo support to provide better scaling and to ensure that this builds and runs seamlessly across Windows, Mac, and Linux.  This first platform was chosen to allow me to learn the tool chains required to get .NET web applications running on a Linux environment with .NET Core.

## How the System Is Designed to be Deployed

The intent is to have this server run inside a set of docker images:

 - MySQL 8 (one)
 - SGAPI (one or more)
 - SGWEB (one or more)

Both SGAPI and SGWEB are stateless.

The MySQL container should publish its MySQL port 3306 on the docker local network only to the SGAPI image.  This prevents any other program accessing it and limits access to the system to route via SGAPI.  

The SGAPI container should expose port 5001 to the SGWEB images directly and to the web mapping to any port you like except HTTP or HTTPS.  Other apps can access SGAPI to work with the forum programmatically over this port.  If no other applications will access the forum then don't expose this port to the web.

The SGWEB images should expose themselves to the web using HTTP and HTTPS ports.

## Setup for Development

### Install .NET Core 2.2

Follow the instructions on the Microsoft site

### Install Visual Studio Code

Follow the instructions on the Microsoft site

### Install MySQL 8

Install MySQL 8 and create an administrator account with a password.  **IMPORTANT:**  At the moment the system just connects using the account `root` and password `pwd`.  This will be updated to take these settings from the `IConfiguration` instance before Alpha.

### Configure Development Certificates

This step assumes you want to develop against the HTTPS stack.  If you're happy to use plain HTTP then you can skip this but must modifiy the configuration files to match.  However doing it this way prevents accidental activation of plain HTTP at the end of your deployment chain.

First step is creating trusted developer certificates.  By default on Linux Chrome will refuse to play with the default certificates.  A test certificate is provided in the `/certs` directory.

Install the `libnss3-tools`: 
`sudo apt install libnss3-tools`

Change the terminal directory to the `certs` directory.  Add the provided certificate locally as a trusted authority for the current user: 
`certutil -d sql:$HOME/.pki/nssdb -A -t "P,," -n sgforum -i sgforum.crt`

Make the certificate trusted by the machine:
`sudo cp sgforum.crt /usr/local/share/ca-certificates/`
`sudo update-ca-certificates`

Add the certificate to Kestrel:

In `appsettings.json` for both SGAPI and SGWEB add the following config section adjusting the path and password to your certificate:

```
"Kestrel": {
    "Certificates": {
      "Default": {
        "Path": "../certs/sgforum.pfx",
        "Password": "pwd"
      }
    }
  }
```

### Configure For First Run

When the SGAPI first runs it will automatically create a database called `SGForum` if it doesn't exist.  If you want to pre-populate it with a bunch of test data useful for development then add the following to the `appsettings.json` file:
`"createTestData" : "yes"`.  This means you can restore your development environment with test data available at any time by simply dropping the SGForum database.

Whichever way this starts, a default administrator user is automatically created called `admin@localhost` and a password `password`.  The administrator is expected to change the default password immediately after the first run in a production environment.

## Running the server from a terminal

Note that if you want to run SGWEB, ensure that SGAPI is already running first.

To run the SGAPI and SGWEB open a terminal, navigate to the project directory `SGAPI` or `SGWEB`.  Type:
`dotnet build`
then
`dotnet run`

Press Ctrl-C in the terminal to stop the application.

## Running the server in Visual Studio Code

Select either SGAPI or SGWEB as the active project first.  Ensure the run target is correct.  Start the debugger. 
