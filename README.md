> ⚠️ This project has been archived since it can no longer serve to reliably track GitHub traffic (which was the primary reason it was created for). Please use [GitHub traffic analytics](https://github.com/blog/1672-introducing-github-traffic-analytics) instead.

# Google Analytics Beacon Service for ASP.NET Core

This is a port of [igrigorik/ga-beacon](https://github.com/igrigorik/ga-beacon) to ASP.NET Core 2.1.

The beacon app serves either a one-pixel transparent GIF image or a visible icon ([once their respective images are created](https://github.com/Kentico/google-analytics-beacon-net/issues/2)). The `<img />` tag with the pixel or icon can be placed to pages that cannot be tracked with ordinary Google Analytics JavaScript code. The beacon service will log hits to such pages instead.

The beacon service will check for the existence of the `cid` cookie (used by Google Analytics) and will create one eventually. 

## Features

The app consists of a Web API controller that accepts either:

* https://beacon-service/api/UA-00000-0?useReferer, or
* https://beacon-service/api/UA-00000-0/relative/path/to/tracked/page

The first option requires a `Referer` request HTTP header to be present.

The above URLs can also be suffixed with the same switches as the [original implementation](https://github.com/igrigorik/ga-beacon/blob/master/ga-beacon.go#L157):

* `pixel`
* `gif`
* `flat`
* `flat-gif`

**Note:** Currently, only the `pixel` switch is supported. Other switches require their respective return images [to be created for this repository](https://github.com/Kentico/google-analytics-beacon-net/issues/2). Feel free to contribute with your images!

![Analytics](https://kentico-ga-beacon.azurewebsites.net/api/UA-69014260-4/Kentico/google-analytics-beacon-net?pixel)
