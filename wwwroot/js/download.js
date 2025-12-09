window.downloadFile = (filename, base64Content) => {
    try {
        const link = document.createElement('a');
        link.href = 'data:application/octet-stream;base64,' + base64Content;
        link.download = filename || 'download';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (e) {
        console.error('downloadFile error', e);
    }
};
