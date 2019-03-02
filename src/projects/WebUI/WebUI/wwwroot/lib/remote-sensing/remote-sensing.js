var app = angular.module("fileUpload", ["xeditable", "ui.select", "ngMaterial", 'ngMessages', "ngFileUpload"]);

app.run(function (editableOptions) {
    editableOptions.theme = 'bs3';
});

app.controller('MyCtrl', ["$scope", "$filter", "$http", "$log", "$timeout", "Upload", function ($scope, $filter, $http, $log, $timeout, Upload) {

    $scope.status = { selectPrefix: false };
    $scope.selection = { row: 0, col: 0, tag: "image" };
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
    }

    $scope.getCurrent = function ( onCompletion ) {
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

    $scope.formImageUrl = function ( file ) {
        var metadataUrl = [$scope.current.cdn, $scope.current.prefix, file ].join('/')
        return metadataUrl;
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
        var imageSelected = oneimage[$scope.selection.tag];
        var imageUrl = $scope.formImageUrl(imageSelected);
        $scope.currentImage = imageUrl;
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
        file.upload = Upload.upload({
            url: '../Recog/Recog/RecogImage',
            data: { file: file },
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