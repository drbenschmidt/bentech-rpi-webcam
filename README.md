# bentech-rpi-webcam

## Description
This is an experiment to use a Raspberry Pi 3 to record chunks of video, while exposing a way to view a stream of images for mobile viewing. _This doesn't do anything cool like writing a custom sink to split off video capture for RTMP_, it's really just a way for me to write a bunch of code at home.

## TODOs
- [x] Initial Checkin
- [x] Benchmark encoding process and "streaming" to see if it can even work on an rPi3
- [ ] Refactor codebase to support taking pictures from a device that supports simultanious audio capture
- [ ] Also determine if the above idea would even work, and see how long audio drift might become
- [ ] Implement scheduling service to have jobs run when needed
- [ ] Implement own JSON serializer, you know, for fun
- [ ] Implement own SQL style database, you know, to hate myself
- [ ] Implement socket based real-time RPC framework, again, because why not
- [ ] Fully plumb logging + add custom logging to central database (for multiple cameras)
- [ ] Organize and comment code so people on GitHub don't make fun of me
- [ ] Build server component for intranet, give it some security
- [ ] Build out HTTP server to support more features, maybe move it into it's on repo.

## Captains Log
03/25/18 - Turns out the MP4 encoder doesn't like settings low enough for the raspberry pi to handle. I could get a 320x240@5fps ~500kbps but I'd rather have it take a picture every 200ms and copy them to a home server to eventually be stitched together into a video with ffmpeg. Time to go crazy with changes!
