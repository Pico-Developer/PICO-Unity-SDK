adb root
adb shell am force-stop com.spatialadapter.server
adb uninstall com.spatialadapter.server
adb install -r SpatialAdapterServer.apk
adb shell am start -n com.spatialadapter.server/.MainActivity
