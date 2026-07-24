#!/bin/bash

# Default values
PROJ_DIR=""
UNITY_VERSION=""
UNITY_COMMAND=""

# Parse named arguments
while [ "$1" != "" ]; do
    case $1 in
        -p | --package )         shift
                                 PROJ_DIR=$1
                                 ;;
        -v | --version )         shift
                                 UNITY_VERSION=$1
                                 ;;
        -u | --unity )           shift
                                 UNITY_COMMAND=$1
                                 ;;
        -h | --help )            echo "Usage: $0 -p <project_path> -v <unity_version> or -u <unity_path>"
                                 exit 0
                                 ;;
        * )                      echo "Invalid parameter: $1"
                                 echo "Usage: $0 -p <project_path> -v <unity_version> or -u <unity_path>"
                                 exit 1
    esac
    shift
done

# Check if both arguments are provided
if [[ -z "$PROJ_DIR" || ( -z "$UNITY_VERSION" && -z "$UNITY_COMMAND" ) ]]; then
    echo "Usage: $0 -p <package_path> -v <unity_version> or -u <unity_path>"
    exit 1
fi

if [ -n "$UNITY_VERSION" ]; then
    # Construct the command
    if [[ "$OSTYPE" == "darwin"* ]]; then
        UNITY_COMMAND="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
    elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
        UNITY_COMMAND="C:\\Program Files\\Unity\\Hub\\Editor\\$UNITY_VERSION\\Editor\\Unity.exe"
    else
        echo "Unsupported OS, please specify Unity path with -u instead"
        exit 1
    fi
fi

echo "Building project: $PROJ_DIR"

"$UNITY_COMMAND" \
  -projectPath $PROJ_DIR \
  -buildTarget Android \
  -executeMethod BuildAutomator.BuildAndProfile \
  -logFile build.log
