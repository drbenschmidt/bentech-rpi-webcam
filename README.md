# bentech-rpi-webcam

## Description
This is an experiment to use a Raspberry Pi 3 to record chunks of video, while exposing a way to view a stream of images for mobile viewing. _This doesn't do anything cool like writing a custom sink to split off video capture for RTMP_, it's really just a way for me to write a bunch of code at home.

## TODOs
- [x] Initial Checkin
- [ ] Benchmark encoding process and "streaming" to see if it can even work on an rPi3
- [ ] Fully plumb logging + add custom logging to central database (for multiple cameras)
- [ ] Organize and comment code so people on GitHub don't make fun of me
- [ ] Build server component for intranet, give it some security
- [ ] Build out HTTP server to support more features, maybe move it into it's on repo.
