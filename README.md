# PixilationRenderFeature
Code for a pixilation render feature effect for Unity URP, version for 2021 and 2022

Example at https://youtu.be/Xmy504dTwoY?si=EtZCpttk0PnDgg6m

![alt text](https://github.com/FeralPug/PixilationRenderFeature/blob/main/Example/Pixilation_Moment.jpg)

Download the correct version of the script and shader to a Unity project using URP. Ther version does matter as Unity changed the Scriptable Render Feature and Pass classes in 2022. The 2021 version will not work in 2022 and vice a versa. The 2022 version has some commented out code that are left overs from the 2021 version.

Add the render feature to your URP pipeline. Create the material and assign the shader. Then any objects in the assigned layer will get pixilated based on the settings in the render feature. It is not perfect but it works pretty good.
