var app = angular.module("fileUpload", ["xeditable", "ui.select", "ngMaterial", 'ngMessages', "ngFileUpload"]);

app.run(function (editableOptions) {
    editableOptions.theme = 'bs3';
});

app.controller('MyCtrl', ["$scope", "$filter", "$http", "$log", "$timeout", "Upload", function ($scope, $filter, $http, $log, $timeout, Upload) {

    $scope.status = { selectPrefix: false };
    $scope.selection = {
        row: 0, col: 0, tag: "image", operation: {}
    };
    $scope.metadata = {};

    $scope.hasObject = function (obj) {
        if (obj == null) {
            return false;
        }
        for (var prop in obj) {
            if (obj.hasOwnProperty(prop))
                return true;
        }
        return false;
    };

    $scope.toggle = function (item, dic) {
        if (dic.hasOwnProperty(item)) {
            dic[item] = !dic[item];
        } else {
            dic[item] = true;
        };
    };

    $scope.status = function (item, dic) {
        if (dic.hasOwnProperty(item)) {
            return dic[item];
        } else {
            return false;
        }
    };

    $scope.clearResult = function () {
        delete $scope.errorMsg;
    };

    $scope.getCurrent = function (onCompletion) {
        $http.get('/api/Image/GetCurrent').then(function (response) {
            var current = response.data;
            $scope.current = response.data;
            $log.log($scope.current);
            if (onCompletion != null) {
                onCompletion();
            };
        });
    }

    $scope.onSuccess = function (tag, response, onCompletion) {
        if (response.data.error) {
            alert(tag + " Error: " + data.error);
        }
        $log.log(tag + " Success " + response.data);
        onCompletion(response.data);
    };

    $scope.onError = function (response) {
        alert('post request failed, check server?');
    }

    $scope.selectPrefixOld = function ( onCompletion ) {
        var post = $scope.current;
        var postInfo = $http.post('/api/Image/SelectPrefix', post);

        var successFunc = function (response) {
            $scope.onSuccess("selectPrefix", response, data => {
                $scope.selectedPrefix = $scope.current.prefix;
                $scope.prefixSelections = data.prefix;
                $scope.status.selectPrefix = true;
            });
            if (onCompletion != null) {
                onCompletion();
            };
        };

        postInfo.then(successFunc, $scope.onError);
    }

    $scope.selectPrefix = function (onCompletion) {
        var indexUrl = $scope.formUrl("index", "index.json");
        $http.get(indexUrl).then(function (response) {
            $scope.selectedPrefix = response.data.prefix;
            $scope.prefixSelections = response.data.prefix;
            $scope.status.selectPrefix = true;
            if (onCompletion != null) {
                onCompletion();
            };
        });
    }

    $scope.doneSelectPrefix = function () {
        $scope.downloadImageMetadata();
        // $scope.status.selectPrefix = false;
    }

    $scope.formImageUrl = function (file) {
        var metadataUrl = [$scope.current.cdn, $scope.current.prefix, file].join('/')
        return metadataUrl;
    }

    $scope.formUrl = function (prefix, file) {
        var metadataUrl = [$scope.current.cdn, prefix, file].join('/')
        return metadataUrl;
    }


    $scope.form_image_name = function (file, tag) {
        var prefix = file.split(".")[0];
        if (tag == "overlay") {
            return "overlay_" + file;
        } else if (tag == "seg") {
            return "seg_" + prefix + ".png";
        } else {
            return file; 
        }
    }

    $scope.downloadImageMetadata = function (onCompletion) {
        if ($scope.current.prefix == null || 0 == $scope.current.prefix.length ) {
            return;
        }
        var metadataUrl = $scope.formImageUrl("metadata.json");
        $http.get(metadataUrl).then(function (response) {
            $scope.metadata = response.data;
            $log.log($scope.metadata);
            if (onCompletion != null) {
                onCompletion();
            } else {
                $scope.showImage();
            };
        });
    }

    $scope.showImage = function () {
        var selectrow = $scope.selection.row; 
        if (selectrow == null) { selectrow = 0 };
        if (selectrow >= $scope.metadata.images.length) {
            selectrow = $scope.metadata.images.length - 1;
        }
        var imagerow = $scope.metadata.images[selectrow];
        var selectcol = $scope.selection.col; 
        if (selectcol == null) { selectcol = 0 };
        if (selectcol >= imagerow.length) {
            selectcol = imagerow.length - 1;
        }
        var oneimage = imagerow[selectcol];
        var imgname = oneimage["image"];
        var onemeta = {};
        onemeta["latitude"] = oneimage["lat"];
        onemeta["longitude"] = oneimage["lon"];
        var imageSelected = $scope.form_image_name(imgname, $scope.selection.tag);
        var imageUrl = $scope.formImageUrl(imageSelected);
        $scope.currentImage = imageUrl;
        $scope.onemeta = JSON.stringify(onemeta); 
    }

    $scope.canUp = function () {
        return $scope.selection.row > 0;
    }

    $scope.moveUp = function () {
        if ( $scope.canUp() ) {
            $scope.selection.row -= 1;
        }
        $scope.showImage();
    }

    $scope.canDown = function () {
        return $scope.selection.row < $scope.metadata.images.length - 1;
    }

    $scope.moveDown = function () {
        if ( $scope.canDown() ) {
            $scope.selection.row += 1;
        }
        $scope.showImage();
    }

    $scope.canLeft = function () {
        return $scope.selection.col > 0;
    }

    $scope.moveLeft = function () {
        if ($scope.canLeft()) {
            $scope.selection.col -= 1;
        }
        $scope.showImage();
    }

    $scope.canRight = function () {
        return $scope.selection.col < $scope.metadata.images[$scope.selection.row].length - 1;
    }

    $scope.moveRight = function () {
        if ($scope.canRight()) {
            $scope.selection.col += 1;
        }
        $scope.showImage();
    }


    $scope.changeCurrent = function () {
        $log.log($scope.current);
        var post = $scope.current;
        var postInfo = $http.post('/api/Image/SetCurrent', post);

        var successFunc = function (response) {
            $scope.onSuccess("changeCurrent", response, data => $scope.current = data );
        };

        postInfo.then(successFunc, $scope.onError );
    };


    $scope.submit = function () {
        if ($scope.form.file.$valid && $scope.file) {
            $scope.upload($scope.file);
        }
    };

    $scope.clearResult = function () {
        $scope.info = [];
        delete $scope.errorMsg;
    };

    $scope.uploadPic = function (file) {
        var postdata = {
            file: file,
            prefix: $scope.current.prefix,
            row: $scope.selection.row,
            col: $scope.selection.col,
            operation: $scope.selection.operation
        };
        file.upload = Upload.upload({
            url: 'api/Recog/UploadImage',
            data: postdata,
        });

        // upload on file select or drop
        file.upload.then(function (response) {
            $scope.imagename = response.data.result.file.output;
            $scope.info = $scope.imagename.split("\n").filter(function (el) { return el.length != 0 });
        }, function (response) {
            if (response.status > 0)
                $scope.errorMsg = response.status + ': ' + response.data;
        }, function (evt) {
            // Math.min is to fix IE which reports 200% sometimes
            file.progress = Math.min(100, parseInt(100.0 * evt.loaded / evt.total));
        });
    };

    $scope.launchMap = function (lat, lon, level) {
        
        var map = new Microsoft.Maps.Map('#myMap', {
            credentials: "ArVGTwXaB6QlXN1XMCGlIgjnXcOGRUhHUw6_gIgU2k62v9D8YS8Imk_97jPzxW73",
            center: new Microsoft.Maps.Location(lat, lon),
            mapTypeId: Microsoft.Maps.MapTypeId.aerial,
            zoom: level
        });

        $scope.map = map; 
        //Create some shapes to convert into GeoJSON.
        var polygon = new Microsoft.Maps.Polygon([
            [new Microsoft.Maps.Location(41, -104.05),
            new Microsoft.Maps.Location(45, -104.05),
            new Microsoft.Maps.Location(45, -111.05),
            new Microsoft.Maps.Location(41, -111.05),
            new Microsoft.Maps.Location(41, -104.05)]]);

        var pin = new Microsoft.Maps.Pushpin(new Microsoft.Maps.Location(43, -107.55));

        //Create an array of shapes.
        var shapes = [polygon, pin];

        //Add the shapes to the map so we can see them (optional).
        map.entities.push(shapes);

        Microsoft.Maps.loadModule('Microsoft.Maps.GeoJson', function () {
            //Convert the array of shapes into a GeoJSON object.
            // var geoJson = Microsoft.Maps.GeoJson.write(shapes);

            //Display the GeoJson in a new Window.
            // var myWindow = window.open('', '_blank', 'scrollbars=yes, resizable=yes, width=400, height=100');
            // myWindow.document.write(JSON.stringify(geoJson));
        });
    };

    $scope.doneSelectPrefixBrowseContour = function () {
        $scope.mapkey = "contour";
        $scope.mapext = ".json";
        $scope.doneSelectPrefixBrowseMap();
    };

    $scope.doneSelectPrefixBrowseMap = function () {
        $scope.downloadImageMetadata($scope.downloadAdditionalMetadata);
    };

    $scope.downloadAdditionalMetadata = function () {
         
        var onemeta = {};
        onemeta["minlat"] = $scope.metadata.minlat;
        onemeta["maxlat"] = $scope.metadata.maxlat;
        onemeta["minlon"] = $scope.metadata.minlon;
        onemeta["maxlon"] = $scope.metadata.maxlon;
        onemeta["level"] = $scope.metadata.level; 
        var lat = ($scope.metadata.minlat + $scope.metadata.maxlat) / 2.0; 
        var lon = ($scope.metadata.minlon + $scope.metadata.maxlon) / 2.0; 
        var level = $scope.metadata.level; 
        $scope.launchMap(lat, lon, level);
        $scope.onemeta = JSON.stringify(onemeta);
        $scope.totalToLoad = 0;
        $scope.totalLoaded = 0;
        $scope.dataBounds == null;
        $scope.map.entities.clear(); 
        var imgarr = $scope.metadata.images;
        for (tiley = 0; tiley < imgarr.length; tiley++) {
            for (tilex = 0; tilex < imgarr[tiley].length; tilex++) {
                $scope.totalToLoad += 1; 
                try {
                    var oneimage = imgarr[tiley][tilex];
                    var imgname = oneimage.image;
                    var basename = imgname.split(".")[0];
                    var geojson_url = $scope.formImageUrl($scope.mapkey + "_" + basename + $scope.mapext);
                    $http.get(geojson_url).then(function (response) {
                        $scope.totalLoaded += 1; 
                        var onegeojson = response.data;

                        var shapes = [];
                        // Interpret geojson
                        // var polygons = onegeojson.


                        // var geoJsonText = JSON.stringify(onegeojson);
                        var shapes = Microsoft.Maps.GeoJson.read(onegeojson, {
                            polygonOptions: {
                                fillColor: 'rgba(0,0,30,0.0)',
                                strokeColor: 'blue',
                                strokeThickness: 1
                            }
                        });
                        $scope.map.entities.push(shapes);
                        var geoJson = Microsoft.Maps.GeoJson.write(shapes);

                        var bounds = Microsoft.Maps.LocationRect.fromShapes(shapes);
                        if ($scope.dataBounds) {
                            $scope.dataBounds = Microsoft.Maps.LocationRect.merge($scope.dataBounds, bounds);
                        } else {
                            $scope.dataBounds = bounds;
                        }
                        onemeta["bounds"] = $scope.dataBounds;
                        // if ($scope.totalToLoad > $scope.totalLoaded - 5) {
                        //    $scope.map.setView({ bounds: $scope.dataBounds, padding: 50 });
                        // };
                    });
                    
                } catch(err) {

                };
            };
        };
    };

    $scope.getCurrent($scope.downloadImageMetadata);

}]);