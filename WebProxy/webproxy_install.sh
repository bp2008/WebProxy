#!/bin/bash

AppName="webproxy"
GithubRepo="bp2008/WebProxy"
ExeName="WebProxyLinux.dll"
AssemblyName="WebProxyLinux"

################################################################
# Function: Check if a package exists, and if not, install it.
# Argument 1: Package Name
# Argument 2: Pass "update" to update the package list before 
#             installing the package. This ensures that the 
#             latest version gets installed.
################################################################
InstallNet6IfNotAlready () {
	# Check if .NET 10.0 is already installed
	if ! dotnet --list-runtimes | grep -q 'Microsoft.NETCore.App 10.0'; then
	    # Install .NET 10.0 from OS-provided packages or from Microsoft's packages
	    if [ -f /etc/os-release ]; then
	        # shellcheck disable=SC1091
	        . /etc/os-release
	        if [ "$ID" == "ubuntu" ] && [ "${VERSION_ID%.*}" -ge "22" ]; then
	            sudo apt-get update
	            # Ubuntu 24.04+ ships dotnet-runtime-10.0 directly in its own archive.
	            # Ubuntu 22.04's archive does not have it yet, so fall back to Canonical's
	            # .NET backports PPA, but only if the package isn't already available.
	            if ! apt-cache show dotnet-runtime-10.0 &> /dev/null; then
	                if [ "${VERSION_ID%.*}" -eq "22" ]; then
	                    echo "dotnet-runtime-10.0 not found in the default repositories; adding ppa:dotnet/backports"
	                    if ! command -v add-apt-repository &> /dev/null; then
	                        sudo apt-get install -y software-properties-common
	                    fi
	                    sudo add-apt-repository -y ppa:dotnet/backports
	                    sudo apt-get update
	                else
	                    echo "dotnet-runtime-10.0 was not found in the default repositories for this Ubuntu release"
	                    exit 1
	                fi
	            fi
	            sudo apt-get install -y dotnet-runtime-10.0
	        elif [ "$ID" == "rhel" ] && [ "${VERSION_ID%.*}" -ge "8" ]; then
	            # .NET 10.0 is included in the RHEL AppStream repositories; no extra repo needed.
	            sudo yum install -y dotnet-sdk-10.0
	        elif [ "$ID" == "amzn" ] && [ "$VERSION_ID" != "2" ]; then
	            # Amazon Linux 2023+ ships dotnet-sdk-10.0 directly; no Microsoft repo needed.
	            sudo yum install -y dotnet-sdk-10.0
	        else
	            echo "Unsupported Linux distribution for installing .NET 10.0 from OS-provided packages"
	            exit 1
	        fi
	    else
	        echo "Unsupported Linux distribution for installing .NET 10.0 from OS-provided packages"
	        exit 1
	    fi
	fi
}

##################################################################
# Function: Check if jq is installed, and if not, install it.
##################################################################
InstallJqIfNotAlready () {
    # Check if jq is already installed.
    if ! command -v jq &> /dev/null; then
        # Install jq using the package manager.
        if [ -f /etc/os-release ]; then
            # shellcheck disable=SC1091
            . /etc/os-release;
            if [ "$ID" == "ubuntu" ] || [ "$ID" == "debian" ]; then
                sudo apt-get update;
                sudo apt-get install -y jq;
            elif [ "$ID" == "rhel" ] || [ "$ID" == "centos" ] || [ "$ID" == "fedora" ] || ([ "$ID" == "amzn" ] && [ "${VERSION_ID%.*}" -ge "2" ]); then
                sudo yum install -y jq;
            elif [ "$ID" == "arch" ]; then
                sudo pacman -Sy jq;
            else
                echo "Unsupported Linux distribution for installing jq";
                exit 1;
            fi;
        else
            echo "Unsupported Linux distribution for installing jq";
            exit 1;
        fi;
    fi;
}

##################################################################
# Function: Check if unzip is installed, and if not, install it.
##################################################################
InstallUnzipIfNotAlready () {
    # Check if unzip is already installed.
    if ! command -v unzip &> /dev/null; then
        # Install unzip using the package manager.
        if [ -f /etc/os-release ]; then
            # shellcheck disable=SC1091
            . /etc/os-release;
            if [ "$ID" == "ubuntu" ] || [ "$ID" == "debian" ]; then
                sudo apt-get update;
                sudo apt-get install -y unzip;
            elif [ "$ID" == "rhel" ] || [ "$ID" == "centos" ] || [ "$ID" == "fedora" ] || ([ "$ID" == "amzn" ] && [ "${VERSION_ID%.*}" -ge "2"]); then 
                sudo yum install -y unzip;
            elif [ "$ID" == "arch" ]; then
                sudo pacman -Sy unzip;
            else
                echo "Unsupported Linux distribution for installing unzip";
                exit 1;
            fi;
        else
            echo "Unsupported Linux distribution for installing unzip";
            exit 1;
        fi;
    fi;
}

#########################################
# Uninstallation.
#########################################

uninstallApp () {
	echo Uninstalling $AppName.
	echo "The .NET 10.0 runtime and other dependencies will remain installed."
	echo "The application's settings, logs, and other generated files remain in \"/usr/share/$AssemblyName\" and may be deleted manually if you wish."
	
	cd ~
    sudo /usr/bin/dotnet "$(pwd)/$AppName/$ExeName" uninstall
	sudo rm -r -f "$AppName"
}

#########################################
# Installation.
#########################################

installAndRun () {

echo Beginning installation of $AppName.
echo To uninstall, run this script with the argument "-u"

#########################################
echo Step 1/5: Install .NET 10.0 runtime.
#########################################

InstallNet6IfNotAlready

#########################################
echo Step 2/5: Install jq if necessary.
#########################################

InstallJqIfNotAlready

#########################################
echo Step 3/5: Install unzip if necessary.
#########################################

InstallUnzipIfNotAlready

##########################################################
echo Step 4/5: Download and extract the latest release.
##########################################################

# Navigate to the home directory.
cd ~;

# Get the release information from the GitHub API and extract the download URL for the Linux release zip using jq.
Releases=$(curl -s https://api.github.com/repos/"$GithubRepo"/releases | jq -r '.[] | .tag_name' | head -n 20);

# Display the list of releases to the user.
echo "Available releases:";
counter=1;
while read -r line; do
    echo "$counter) $line";
    counter=$((counter+1));
done <<< "$Releases"

# Prompt the user to choose a release.
read -p "Enter the number of the release you want to install (default is 1): " ReleaseChoice;

# Set default choice to 1 if no input is given.
if [ -z "$ReleaseChoice" ]; then
    ReleaseChoice=1;
fi

# Get the tag name of the chosen release.
ReleaseTag=$(echo "$Releases" | sed "${ReleaseChoice}q;d");

echo "Installing Release $ReleaseTag"

# Get the download URL for the chosen release.
ReleaseUrl=$(curl -s https://api.github.com/repos/"$GithubRepo"/releases/tags/"$ReleaseTag" | jq -r '.assets[] | select(.name | contains("Linux")) | .browser_download_url');

# Set the release file name to the variable "ReleaseFile".
ReleaseFile=${ReleaseUrl##*/};

# Download the chosen release using wget.
wget -q -O "$ReleaseFile" $ReleaseUrl;

# Ensure that the application directory exists.
mkdir -p "$AppName";

# Unzip the release using unzip.
unzip -q -o $ReleaseFile -d "$AppName";



###########################################################
echo Step 5/5: Configure program to start automatically.
###########################################################

echo Creating $AppName service: sudo /usr/bin/dotnet \"$(pwd)/$AppName/$ExeName\" install
sudo /usr/bin/dotnet "$(pwd)/$AppName/$ExeName" install

echo Starting $AppName service: sudo /usr/bin/dotnet \"$(pwd)/$AppName/$ExeName\" restart
sudo /usr/bin/dotnet "$(pwd)/$AppName/$ExeName" restart


}

######################
# Decide what to do.
######################

if [ "$1" = "-i" ]; then
	installAndRun;
elif [ "$1" = "-u" ]; then
	uninstallApp;
else
	echo "This is the $AppName installer. Choose an option:";
	echo # line break;
	COLUMNS=12 # I hate linux;
	select choice in "Install/Update and run $AppName" "Uninstall $AppName" "Cancel";
	do
		case $choice in
			Install* ) installAndRun;break;;
			Uninstall* ) uninstallApp;break;;
			Cancel ) exit;;
		esac
	done
fi

