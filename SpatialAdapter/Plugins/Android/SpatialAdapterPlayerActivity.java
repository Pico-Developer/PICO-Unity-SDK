package com.unity3d.player;

import com.unity3d.player.UnityPlayerActivity;
import android.os.Bundle;
import android.util.Log;

import android.view.Window;
import android.view.WindowManager;
import android.graphics.drawable.ColorDrawable;

public class SpatialAdapterPlayerActivity extends UnityPlayerActivity
{
    private static SpatialAdapterPlayerActivity instance;
    public static SpatialAdapterPlayerActivity getInstance()
    {
        return instance;
    }

    protected void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);
        
        Window window = getWindow();
        window.addFlags(
            WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE |
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
        );
        
        WindowManager.LayoutParams params = window.getAttributes();
        params.alpha = 0.0f;
        window.setAttributes(params);

        instance = this;
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();

        instance = null;
    }

    // TODO: Remove the following once transparent apps receive focus events
    
    protected void onResume() {
        super.onResume();
        super.onWindowFocusChanged(true);
    }

    @Override
    protected void onPause() {
        super.onPause();
        super.onWindowFocusChanged(false);
    }

    @Override
    public void onWindowFocusChanged(boolean bl) {
        // super.onWindowFocusChanged(bl);
    }

    // End of TODO
}
