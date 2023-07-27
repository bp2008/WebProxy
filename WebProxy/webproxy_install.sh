#!/bin/bash

AppName="webproxy"
GithubRepo="bp2008/WebProxy"
ExeName="WebProxyLinux.dll"

################################################################
# Function: Check if a package exists, and if not, install it.
# Argument 1: Package Name
# Argument 2: Pass "update" to update the package list before 
#             installing the package. This ensures that the 
#             latest version gets installed.
################################################################
InstallNet6IfNotAlready () {
	# Check if .NET 6.0 is already installed
	if ! dotnet --list-runtimes | grep -q 'Microsoft.NETCore.App 6.0'; then
	    # Install .NET 6.0 from OS-provided packages or from Microsoft's packages
	    if [ -f /etc/os-release ]; then
	        # shellcheck disable=SC1091
	        . /etc/os-release
	        if [ "$ID" == "ubuntu" ] && [ "${VERSION_ID%.*}" -ge "22" ]; then
	            sudo apt-get update
	            sudo apt-get install -y dotnet-runtime-6.0
	        elif [ "$ID" == "rhel" ] && [ "${VERSION_ID%.*}" -ge "8" ]; then
	            sudo yum install -y dotnet-sdk-6.0
	        elif [ "$ID" == "amzn" ] && [ "${VERSION_ID%.*}" -ge "2" ]; then
	            sudo yum update
	            sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
	            sudo yum install dotnet-sdk-6.0
	        else
	            echo "Unsupported Linux distribution for installing .NET 6.0 from OS-provided packages"
	            exit 1
	        fi
	    else
	        echo "Unsupported Linux distribution for installing .NET 6.0 from OS-provided packages"
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
	echo "The .NET 6.0 runtime will remain installed."
	
	cd ~
	sudo rm -r -f "$AppName"
	sudo rm -f "/etc/systemd/system/$AppName.service"
}

#########################################
# Installation.
#########################################

installAndRun () {

echo Beginning installation of $AppName.
echo To uninstall, run this script with the argument "-u"

#########################################
echo Step 1/5: Install .NET 6.0 runtime.
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
ReleaseUrl=$(curl -s https://api.github.com/repos/"$GithubRepo"/releases/latest | jq -r '.assets[] | select(.name | contains("Linux")) | .browser_download_url');

# Set the release file name to the variable "ReleaseFile".
ReleaseFile=${ReleaseUrl##*/};

# Download the latest release using wget.
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

echo Starting $AppName service: sudo /usr/bin/dotnet \"$(pwd)/$AppName/$ExeName\" start
sudo /usr/bin/dotnet "$(pwd)/$AppName/$ExeName" start


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

