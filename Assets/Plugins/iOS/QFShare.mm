// iPad share sheet for the record file (UIActivityViewController). Called from C# via DllImport("__Internal").
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController();

extern "C" void QFShareFile(const char* path, const char* subject)
{
    if (path == NULL) return;
    NSString* p = [NSString stringWithUTF8String:path];
    NSURL* url = [NSURL fileURLWithPath:p];
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = UnityGetGLViewController();
        if (root == nil) return;
        UIActivityViewController* vc = [[UIActivityViewController alloc] initWithActivityItems:@[url] applicationActivities:nil];
        if (subject != NULL) { @try { [vc setValue:[NSString stringWithUTF8String:subject] forKey:@"subject"]; } @catch (NSException* e) {} }
        // iPad: the share sheet is a popover and needs an anchor, otherwise it crashes
        UIPopoverPresentationController* pop = vc.popoverPresentationController;
        if (pop != nil)
        {
            pop.sourceView = root.view;
            CGRect b = root.view.bounds;
            pop.sourceRect = CGRectMake(b.size.width - 60, 150, 1, 1);
            pop.permittedArrowDirections = UIPopoverArrowDirectionAny;
        }
        [root presentViewController:vc animated:YES completion:nil];
    });
}
