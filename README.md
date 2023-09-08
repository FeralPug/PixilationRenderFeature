# PixilationRenderFeature
Code for a pixilation render feature effect for Unity URP, compatible with 2022

Example at https://youtu.be/Xmy504dTwoY?si=EtZCpttk0PnDgg6m

![alt text](https://github.com/FeralPug/PixilationRenderFeature/blob/main/Example/Pixilation_Moment.jpg)

Download the script and shader to a Unity project using URP 2022. Commented out code in the script is for a version that worked with 2021. Unity changed the way render features work for 2022 so that is why there are those changes. 

Add the render feature to your URP pipeline. Create the material and assign the shader. Then any objects in the assigned layer will get pixilated based on the settings in the render feature. It is not perfect but it works pretty good.
