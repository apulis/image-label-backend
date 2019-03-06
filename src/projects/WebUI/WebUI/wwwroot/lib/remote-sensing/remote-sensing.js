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

    $scope.selectPrefix = function () {
        var post = $scope.current;
        var postInfo = $http.post('/api/Image/SelectPrefix', post);

        var successFunc = function (response) {
            $scope.onSuccess("selectPrefix", response, data => {
                $scope.selectedPrefix = $scope.current.prefix;
                $scope.prefixSelections = data.prefix;
                $scope.status.selectPrefix = true;
            });
        };

        postInfo.then(successFunc, $scope.onError);
    }

    $scope.doneSelectPrefix = function () {
        $scope.downloadImageMetadata();
        // $scope.status.selectPrefix = false;
    }

    $scope.formImageUrl = function (file) {
        var metadataUrl = [$scope.current.cdn, $scope.current.prefix, file].join('/')
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

    $scope.getCurrent($scope.downloadImageMetadata )


}]);